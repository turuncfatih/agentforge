using System.Collections.Concurrent;
using AgentForge.Application.Ports;
using AgentForge.Domain.Delivery;

namespace AgentForge.Infrastructure.Persistence;

/// <summary>
/// The adapter the vertical slice runs on.
///
/// It is in-memory, and that is a scope decision rather than a design one: the
/// port is what the application depends on, so replacing this with EF Core
/// changes one registration and no application code. What it does model
/// faithfully is the optimistic-concurrency check, because losing a concurrent
/// write is a bug that only appears once there is a real database.
/// </summary>
public sealed class InMemoryDeliveryTaskRepository : IDeliveryTaskRepository
{
    private readonly ConcurrentDictionary<TaskId, Entry> _store = new();

    public Task<DeliveryTask?> FindAsync(TaskId id, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(_store.TryGetValue(id, out var entry) ? entry.Task : null);
    }

    public Task SaveAsync(DeliveryTask task, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        _store.AddOrUpdate(
            task.Id,
            _ => new Entry(task, task.Version),
            (_, existing) =>
            {
                if (!ReferenceEquals(existing.Task, task) && existing.Version > task.Version)
                {
                    throw new InvalidOperationException(
                        $"Concurrent modification of task {task.Id}: stored version {existing.Version} is newer than {task.Version}.");
                }

                return new Entry(task, task.Version);
            });

        return Task.CompletedTask;
    }

    private sealed record Entry(DeliveryTask Task, int Version);
}
