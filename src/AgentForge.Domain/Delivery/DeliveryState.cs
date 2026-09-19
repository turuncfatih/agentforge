namespace AgentForge.Domain.Delivery;

/// <summary>
/// Planned -> Executing -> Gating -> (Reworking -> Executing | Approved) -> Reported
/// with Escalated and Failed as terminal exits.
/// </summary>
public enum DeliveryState
{
    Planned,
    Executing,
    Gating,
    Reworking,
    Approved,
    Reported,
    Escalated,
    Failed,
}

public enum GateDecision
{
    Passed,
    Blocked,
}
