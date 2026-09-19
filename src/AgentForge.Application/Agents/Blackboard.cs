using AgentForge.Domain.Delivery;

namespace AgentForge.Application.Agents;

/// <summary>
/// The shared, append-only context agents read from. Agents never call each
/// other directly; they publish here and the orchestrator decides who sees what.
/// That indirection is what keeps the graph loosely coupled and replayable.
/// </summary>
public sealed record Blackboard(
    FeatureRequest Request,
    int Round,
    IReadOnlyList<Artifact> Artifacts,
    IReadOnlyList<Finding> Findings,
    string? ReworkReason)
{
    public static Blackboard Initial(FeatureRequest request) =>
        new(request, Round: 0, Artifacts: [], Findings: [], ReworkReason: null);

    public Blackboard With(IEnumerable<Artifact> artifacts, IEnumerable<Finding> findings) =>
        this with
        {
            Artifacts = [.. Artifacts, .. artifacts],
            Findings = [.. Findings, .. findings],
        };

    public Blackboard ForRework(int round, string reason) =>
        this with { Round = round, ReworkReason = reason };

    /// <summary>Only what the gate objected to. Rework prompts stay small this way.</summary>
    public IReadOnlyList<Finding> BlockingFindings =>
        [.. Findings.Where(f => f.IsBlocking)];
}
