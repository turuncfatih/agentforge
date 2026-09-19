using AgentForge.Application.Agents;
using AgentForge.Domain.Abstractions;
using AgentForge.Domain.Delivery;

namespace AgentForge.Application.Ports;

/// <summary>
/// One repository, for one aggregate. There is deliberately no repository for
/// AgentStep or Finding: they are reached through the task that owns them.
/// </summary>
public interface IDeliveryTaskRepository
{
    Task<DeliveryTask?> FindAsync(TaskId id, CancellationToken cancellationToken);

    Task SaveAsync(DeliveryTask task, CancellationToken cancellationToken);
}

/// <summary>
/// Where drained domain events go: the audit trail, and the live stream the API
/// exposes. Append-only, which is what makes "why did it decide that?" answerable.
/// </summary>
public interface IDeliveryEventSink
{
    Task PublishAsync(TaskId task, IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken);
}

/// <summary>
/// Turns the orchestrator agent's output into a plan the aggregate will accept.
///
/// The model proposes; this port disposes. An implementation must reject a plan
/// that names an unknown agent or appoints a role without veto power as the
/// gate, and fall back to a safe default rather than propagate a bad plan.
/// </summary>
public interface IPlanReader
{
    DeliveryPlan Read(IReadOnlyList<Artifact> artifacts);
}

public interface IAgentRegistry
{
    IAgent Resolve(AgentId id);
}
