using AgentForge.Domain.Abstractions;
using AgentForge.Domain.Delivery.Events;

namespace AgentForge.Domain.Delivery;

/// <summary>
/// The aggregate root, and the only place where delivery rules are enforced.
///
/// Invariants held here:
///   1. No step runs once the budget is exhausted.
///   2. A step settles exactly once, so a replay never double-charges.
///   3. The gate cannot be skipped, and a blocking finding cannot be approved away.
///   4. Rework is bounded; past the limit the task escalates to a human.
///   5. Every transition is legal for the current state, or it throws.
///
/// Time is an explicit input rather than ambient state, so the whole state
/// machine is deterministic and testable without a clock abstraction.
/// </summary>
public sealed class DeliveryTask : AggregateRoot<TaskId>
{
    private readonly List<AgentStep> _steps = [];

    private DeliveryTask(TaskId id, FeatureRequest request, Budget budget)
        : base(id)
    {
        Request = request;
        Budget = budget;
        State = DeliveryState.Planned;
        Consumption = Consumption.Zero;
    }

    public FeatureRequest Request { get; }
    public Budget Budget { get; }
    public DeliveryState State { get; private set; }
    public Consumption Consumption { get; private set; }
    public DeliveryPlan? Plan { get; private set; }

    /// <summary>Zero-based rework round. Part of every step id.</summary>
    public int Round { get; private set; }

    public string? TerminalReason { get; private set; }

    public IReadOnlyList<AgentStep> Steps => _steps;

    public IEnumerable<Finding> AllFindings => _steps.SelectMany(s => s.Findings);

    public IEnumerable<Artifact> AllArtifacts => _steps.SelectMany(s => s.Artifacts);

    public bool IsTerminal => State is DeliveryState.Reported or DeliveryState.Escalated or DeliveryState.Failed;

    // ---------------------------------------------------------------- lifecycle

    public static DeliveryTask Start(TaskId id, FeatureRequest request, Budget budget, DateTimeOffset now)
    {
        var task = new DeliveryTask(id, request, budget);
        task.Raise(new DeliveryStarted(id, request.Title, budget, now));
        return task;
    }

    public void AcceptPlan(DeliveryPlan plan, DateTimeOffset now)
    {
        Transition(from: DeliveryState.Planned, to: DeliveryState.Executing, because: "accept plan");
        Plan = plan;
        Raise(new PlanAccepted(Id, plan, now));
    }

    // ---------------------------------------------------------------- steps

    /// <summary>
    /// True when this agent already produced a settled step in the current round.
    /// The caller checks this before re-running an agent, which is what makes
    /// crash recovery cheap: replay skips work that is already paid for.
    /// </summary>
    public bool HasSettledStep(AgentId agent) =>
        _steps.Any(s => s.Agent == agent && s.Round == Round && s.Status is StepStatus.Settled);

    public StepId OpenStep(AgentId agent, DateTimeOffset now)
    {
        Ensure.That(
            State is DeliveryState.Planned or DeliveryState.Executing or DeliveryState.Gating or DeliveryState.Approved,
            $"Cannot open a step while the task is {State}. "
            + "Planned is allowed so that planning is charged to the budget like any "
            + "other step, and Approved so the reporter can run after the gate passes.");

        var verdict = Consumption.Against(Budget);
        Ensure.That(
            verdict is BudgetVerdict.WithinBudget,
            $"Budget exhausted ({verdict}); call {nameof(EscalateForBudget)} instead of opening another step.");

        var id = StepId.For(Id, agent, Round);
        Ensure.That(
            _steps.All(s => s.Id != id),
            $"Step {id} already exists. Step ids are deterministic, so this is a double-open, not a retry.");

        _steps.Add(AgentStep.Open(id, agent, Round, now));
        Raise(new StepOpened(Id, id, agent, Round, now));
        return id;
    }

    public void SettleStep(
        StepId stepId,
        AgentOutcome outcome,
        IReadOnlyList<Artifact> artifacts,
        IReadOnlyList<Finding> findings,
        TokenUsage tokens,
        Money cost,
        DateTimeOffset now,
        string? failureReason = null)
    {
        var step = _steps.SingleOrDefault(s => s.Id == stepId)
                   ?? throw new DomainException($"Unknown step {stepId}.");

        step.Settle(outcome, artifacts, findings, tokens, cost, failureReason);
        Consumption = Consumption.Add(tokens, cost);
        Raise(new StepSettled(Id, stepId, outcome, tokens, cost, now));

        if (outcome is AgentOutcome.Failed)
        {
            State = DeliveryState.Failed;
            TerminalReason = failureReason;
            Raise(new DeliveryFailed(Id, failureReason ?? "unspecified", now));
        }
    }

    // ---------------------------------------------------------------- gate

    /// <summary>
    /// Runs the gate over the findings raised in the current round.
    /// A blocked gate never approves: it either schedules rework or, once the
    /// rework budget is spent, hands the task to a human.
    /// </summary>
    public GateDecision RunGate(IGatePolicy policy, DateTimeOffset now)
    {
        Ensure.That(Plan is not null, "The gate cannot run before a plan is accepted.");
        Ensure.That(
            State is DeliveryState.Executing or DeliveryState.Gating,
            $"Cannot run the gate while the task is {State}.");
        Ensure.That(
            Plan!.ParallelWorkers.All(HasSettledStep),
            "The gate may only run once every worker in this round has settled.");
        Ensure.That(
            HasSettledStep(Plan.Gate),
            "The gate agent must produce its own step before its findings can be judged.");

        State = DeliveryState.Gating;

        var roundFindings = _steps
            .Where(s => s.Round == Round)
            .SelectMany(s => s.Findings)
            .ToArray();

        var decision = policy.Evaluate(roundFindings);
        var blocking = roundFindings.Count(f => f.Severity >= Severity.Critical);
        Raise(new GateEvaluated(Id, Round, decision, blocking, now));

        if (decision is GateDecision.Passed)
        {
            State = DeliveryState.Approved;
            Raise(new DeliveryApproved(Id, now));
            return decision;
        }

        if (Round >= Budget.MaxReworkLoops)
        {
            Escalate($"gate still blocked after {Round} rework round(s): {policy.Describe()}", now);
            return decision;
        }

        State = DeliveryState.Reworking;
        return decision;
    }

    public void BeginRework(string reason, DateTimeOffset now)
    {
        Transition(from: DeliveryState.Reworking, to: DeliveryState.Executing, because: "begin rework");
        Round++;
        Raise(new ReworkRequested(Id, Round, Ensure.NotBlank(reason, nameof(reason)), now));
    }

    // ---------------------------------------------------------------- exits

    public void Report(DateTimeOffset now)
    {
        Transition(from: DeliveryState.Approved, to: DeliveryState.Reported, because: "publish report");
        Raise(new DeliveryReported(Id, now));
    }

    public void EscalateForBudget(DateTimeOffset now)
    {
        var verdict = Consumption.Against(Budget);
        Ensure.That(verdict is not BudgetVerdict.WithinBudget, "Budget is not exhausted; nothing to escalate.");
        Escalate($"budget exhausted: {verdict}", now);
    }

    public void Escalate(string reason, DateTimeOffset now)
    {
        Ensure.That(!IsTerminal, $"Task is already {State}.");
        State = DeliveryState.Escalated;
        TerminalReason = Ensure.NotBlank(reason, nameof(reason));
        Raise(new DeliveryEscalated(Id, TerminalReason, now));
    }

    // ---------------------------------------------------------------- internals

    private void Transition(DeliveryState from, DeliveryState to, string because)
    {
        Ensure.That(State == from, $"Cannot {because}: expected state {from} but was {State}.");
        State = to;
    }
}
