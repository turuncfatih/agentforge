using AgentForge.Domain.Abstractions;

namespace AgentForge.Domain.Delivery;

/// <summary>
/// A domain service: the rule belongs to the business, but not to any single
/// entity, so it does not live on the aggregate.
/// </summary>
public interface IGatePolicy
{
    GateDecision Evaluate(IReadOnlyList<Finding> findings);

    string Describe();
}

/// <summary>
/// Blocks delivery when any finding reaches the configured severity.
/// Default is Critical: the gate is a safety net, not a style reviewer.
/// </summary>
public sealed class SeverityThresholdGate(Severity threshold) : IGatePolicy
{
    public static IGatePolicy BlockOnCritical { get; } = new SeverityThresholdGate(Severity.Critical);

    public Severity Threshold { get; } = threshold;

    public GateDecision Evaluate(IReadOnlyList<Finding> findings) =>
        findings.Any(f => f.Severity >= Threshold) ? GateDecision.Blocked : GateDecision.Passed;

    public string Describe() => $"block when severity >= {Threshold}";
}

/// <summary>
/// Requires a human before approval regardless of findings. Used in regulated
/// flows where automation may prepare a decision but never take it.
/// </summary>
public sealed class HumanApprovalGate : IGatePolicy
{
    public GateDecision Evaluate(IReadOnlyList<Finding> findings) => GateDecision.Blocked;

    public string Describe() => "always require a human decision";
}
