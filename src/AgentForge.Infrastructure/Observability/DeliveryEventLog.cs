using System.Collections.Concurrent;
using System.Threading.Channels;
using AgentForge.Application.Ports;
using AgentForge.Domain.Abstractions;
using AgentForge.Domain.Delivery;

namespace AgentForge.Infrastructure.Observability;

public sealed record RecordedEvent(int Sequence, string Type, IDomainEvent Payload);

/// <summary>
/// The append-only trail, and the live feed, from the same writes.
///
/// This is the answer to "why did the system decide that?". Nothing is ever
/// updated or removed: the sequence of domain events is the explanation, and
/// it is the same sequence a replay would produce.
/// </summary>
public sealed class DeliveryEventLog : IDeliveryEventSink
{
    private readonly ConcurrentDictionary<TaskId, List<RecordedEvent>> _log = new();
    private readonly ConcurrentDictionary<TaskId, List<Channel<RecordedEvent>>> _subscribers = new();

    public Task PublishAsync(TaskId task, IReadOnlyList<IDomainEvent> events, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var entries = _log.GetOrAdd(task, _ => []);
        var listeners = _subscribers.GetValueOrDefault(task) ?? [];

        lock (entries)
        {
            foreach (var domainEvent in events)
            {
                var recorded = new RecordedEvent(entries.Count + 1, domainEvent.GetType().Name, domainEvent);
                entries.Add(recorded);

                foreach (var listener in listeners)
                {
                    listener.Writer.TryWrite(recorded);
                }
            }
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<RecordedEvent> Read(TaskId task)
    {
        if (!_log.TryGetValue(task, out var entries))
        {
            return [];
        }

        lock (entries)
        {
            return [.. entries];
        }
    }

    /// <summary>
    /// Replays what already happened, then follows along. A client that connects
    /// halfway through a delivery still sees the whole story.
    /// </summary>
    public async IAsyncEnumerable<RecordedEvent> SubscribeAsync(
        TaskId task,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var channel = Channel.CreateUnbounded<RecordedEvent>();
        var listeners = _subscribers.GetOrAdd(task, _ => []);

        lock (listeners)
        {
            listeners.Add(channel);
        }

        try
        {
            foreach (var replayed in Read(task))
            {
                yield return replayed;
            }

            await foreach (var live in channel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                yield return live;
            }
        }
        finally
        {
            lock (listeners)
            {
                listeners.Remove(channel);
            }
        }
    }
}
