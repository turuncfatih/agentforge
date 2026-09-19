using AgentForge.Application.Agents;
using AgentForge.Application.Ports;
using AgentForge.Domain.Delivery;
using Microsoft.Extensions.Logging;

namespace AgentForge.Application.Orchestration;

/// <summary>
/// The supervisor. It sequences the round, but it owns no delivery rules:
/// every decision that could be wrong is delegated to the aggregate or to the
/// gate policy. Read this class as a script, not as a rulebook.
///
/// It is safe to call twice with the same task id. Work that already settled is
/// skipped rather than repeated, so a crashed run resumes instead of restarting.
/// </summary>
public sealed class DeliveryOrchestrator(
    IAgentRegistry agents,
    AgentPipeline pipeline,
    IGatePolicy gate,
    IPlanReader planReader,
    IDeliveryTaskRepository repository,
    IDeliveryEventSink events,
    TimeProvider time,
    ILogger<DeliveryOrchestrator> logger)
{
    public async Task<DeliveryTask> RunAsync(
        TaskId id,
        FeatureRequest request,
        Budget budget,
        CancellationToken cancellationToken)
    {
        var task = await repository.FindAsync(id, cancellationToken).ConfigureAwait(false);

        if (task is null)
        {
            task = DeliveryTask.Start(id, request, budget, time.GetUtcNow());
            await CheckpointAsync(task, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            logger.LogInformation("resuming task {Task} from state {State} at round {Round}", id, task.State, task.Round);
        }

        var board = Rebuild(task);

        // Planning is an agent step like any other: it is metered, retried,
        // traced and charged. A planner that silently burns tokens outside the
        // budget is how "why was the bill so high?" starts.
        if (task.Plan is null)
        {
            board = await RunRoundAsync(task, board, [AgentId.Orchestrator], cancellationToken).ConfigureAwait(false);

            if (!task.IsTerminal)
            {
                task.AcceptPlan(planReader.Read([.. task.AllArtifacts]), time.GetUtcNow());
                await CheckpointAsync(task, cancellationToken).ConfigureAwait(false);
            }
        }

        while (!task.IsTerminal)
        {
            if (OutOfBudget(task))
            {
                task.EscalateForBudget(time.GetUtcNow());
                break;
            }

            var plan = task.Plan!;

            // 1. Workers, in parallel. Steps are opened first so the aggregate
            //    stays single-threaded even though the agent calls are not.
            board = await RunRoundAsync(task, board, plan.ParallelWorkers, cancellationToken).ConfigureAwait(false);
            if (task.IsTerminal)
            {
                break;
            }

            // 2. The gate agent reviews work it did not produce.
            board = await RunRoundAsync(task, board, [plan.Gate], cancellationToken).ConfigureAwait(false);
            if (task.IsTerminal)
            {
                break;
            }

            // 3. The aggregate, not the orchestrator, decides what the gate means.
            var decision = task.RunGate(gate, time.GetUtcNow());
            await CheckpointAsync(task, cancellationToken).ConfigureAwait(false);

            if (decision is GateDecision.Blocked && task.State is DeliveryState.Reworking)
            {
                var reason = Summarise(board.BlockingFindings);
                task.BeginRework(reason, time.GetUtcNow());
                board = board.ForRework(task.Round, reason);
                await CheckpointAsync(task, cancellationToken).ConfigureAwait(false);
                continue;
            }

            if (task.State is DeliveryState.Approved)
            {
                board = await RunRoundAsync(task, board, [plan.Reporter], cancellationToken).ConfigureAwait(false);
                if (task.State is DeliveryState.Approved)
                {
                    task.Report(time.GetUtcNow());
                    await CheckpointAsync(task, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        logger.LogInformation(
            "task {Task} finished as {State} after {Steps} step(s), {Tokens} tokens, {Cost}",
            id, task.State, task.Consumption.Steps, task.Consumption.Tokens.Total, task.Consumption.Cost);

        return task;
    }

    private async Task<Blackboard> RunRoundAsync(
        DeliveryTask task,
        Blackboard board,
        IReadOnlyList<AgentId> group,
        CancellationToken cancellationToken)
    {
        var pending = group.Where(a => !task.HasSettledStep(a)).ToArray();
        if (pending.Length == 0)
        {
            logger.LogDebug("round {Round}: nothing to do for [{Group}], already settled", task.Round, string.Join(", ", group));
            return board;
        }

        // Open every step before any agent runs: the aggregate is mutated only
        // from this thread, while the expensive calls fan out.
        //
        // The budget is checked per step rather than per round. A round opens
        // several steps, so a ceiling can be reached halfway through one, and
        // "check once at the top of the loop" quietly overspends by a whole round.
        var tickets = new List<(AgentId Agent, StepId Step)>(pending.Length);
        var truncated = false;

        foreach (var agent in pending)
        {
            if (OutOfBudget(task))
            {
                truncated = true;
                break;
            }

            tickets.Add((agent, task.OpenStep(agent, time.GetUtcNow())));
        }

        await CheckpointAsync(task, cancellationToken).ConfigureAwait(false);

        if (tickets.Count == 0)
        {
            task.EscalateForBudget(time.GetUtcNow());
            await CheckpointAsync(task, cancellationToken).ConfigureAwait(false);
            return board;
        }

        var runs = tickets.Select(async ticket =>
        {
            var agent = agents.Resolve(ticket.Agent);
            var request = new AgentRequest(
                task.Id,
                ticket.Step,
                ticket.Agent,
                Goal(task, ticket.Agent),
                board,
                agent.Policy);

            var response = await pipeline.ExecuteAsync(agent, request, cancellationToken).ConfigureAwait(false);
            return (ticket.Step, Response: response);
        });

        var settled = await Task.WhenAll(runs).ConfigureAwait(false);

        foreach (var (step, response) in settled)
        {
            task.SettleStep(
                step,
                response.Outcome,
                response.Artifacts,
                response.Findings,
                response.Tokens,
                response.Cost,
                time.GetUtcNow(),
                response.FailureReason);

            board = board.With(response.Artifacts, response.Findings);
        }

        // Work that was already open is always settled and charged before the
        // task escalates, so the ledger matches what actually ran.
        if (truncated && !task.IsTerminal)
        {
            task.EscalateForBudget(time.GetUtcNow());
        }

        await CheckpointAsync(task, cancellationToken).ConfigureAwait(false);
        return board;
    }

    private bool OutOfBudget(DeliveryTask task) =>
        task.Consumption.Against(task.Budget) is not BudgetVerdict.WithinBudget;

    /// <summary>Persist, then publish what happened. Save before publish, so a
    /// crash between the two replays an event rather than losing state.</summary>
    private async Task CheckpointAsync(DeliveryTask task, CancellationToken cancellationToken)
    {
        await repository.SaveAsync(task, cancellationToken).ConfigureAwait(false);
        var drained = task.DequeueEvents();
        if (drained.Count > 0)
        {
            await events.PublishAsync(task.Id, drained, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Reconstructs the shared context from what the aggregate already holds,
    /// which is what makes resuming a partially finished task possible.</summary>
    private static Blackboard Rebuild(DeliveryTask task) =>
        Blackboard.Initial(task.Request) with
        {
            Round = task.Round,
            Artifacts = [.. task.AllArtifacts],
            Findings = [.. task.AllFindings],
        };

    private static string Goal(DeliveryTask task, AgentId agent) =>
        task.Round == 0
            ? $"Round 0 for \"{task.Request.Title}\": produce your contribution as {agent}."
            : $"Round {task.Round} for \"{task.Request.Title}\": address the blocking findings as {agent}.";

    private static string Summarise(IReadOnlyList<Finding> blocking) =>
        blocking.Count == 0
            ? "gate blocked without a blocking finding"
            : string.Join("; ", blocking.Select(f => $"[{f.Severity}] {f.Title}"));
}
