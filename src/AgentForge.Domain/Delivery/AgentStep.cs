using AgentForge.Domain.Abstractions;

namespace AgentForge.Domain.Delivery;

public enum AgentOutcome
{
    Completed,
    NeedsInput,
    Rejected,
    Failed,
}

public enum StepStatus
{
    Open,
    Settled,
}

/// <summary>
/// One agent invocation, as the domain sees it. An entity inside the
/// <see cref="DeliveryTask"/> aggregate: it has identity but no life of its own.
/// Note what is absent: no prompt, no model name, no vendor. The domain records
/// that work happened and what it cost, not how it was produced.
/// </summary>
public sealed class AgentStep
{
    private readonly List<Artifact> _artifacts = [];
    private readonly List<Finding> _findings = [];

    private AgentStep(StepId id, AgentId agent, int round, DateTimeOffset openedAt)
    {
        Id = id;
        Agent = agent;
        Round = round;
        OpenedAt = openedAt;
        Status = StepStatus.Open;
    }

    public StepId Id { get; }
    public AgentId Agent { get; }
    public int Round { get; }
    public DateTimeOffset OpenedAt { get; }
    public StepStatus Status { get; private set; }
    public AgentOutcome? Outcome { get; private set; }
    public TokenUsage Tokens { get; private set; } = TokenUsage.None;
    public Money Cost { get; private set; } = Money.Zero;
    public string? FailureReason { get; private set; }

    public IReadOnlyList<Artifact> Artifacts => _artifacts;
    public IReadOnlyList<Finding> Findings => _findings;

    internal static AgentStep Open(StepId id, AgentId agent, int round, DateTimeOffset now) =>
        new(id, agent, round, now);

    internal void Settle(
        AgentOutcome outcome,
        IReadOnlyList<Artifact> artifacts,
        IReadOnlyList<Finding> findings,
        TokenUsage tokens,
        Money cost,
        string? failureReason)
    {
        Ensure.That(
            Status is StepStatus.Open,
            $"Step {Id} was already settled; replaying it would double-charge the budget.");
        Ensure.That(
            outcome is not AgentOutcome.Failed || !string.IsNullOrWhiteSpace(failureReason),
            "A failed step must carry a reason.");

        Status = StepStatus.Settled;
        Outcome = outcome;
        Tokens = tokens;
        Cost = cost;
        FailureReason = failureReason;
        _artifacts.AddRange(artifacts);
        _findings.AddRange(findings);
    }
}
