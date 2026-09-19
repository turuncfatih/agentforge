namespace AgentForge.Domain.Abstractions;

/// <summary>
/// Consistency boundary. Every state change of the aggregate goes through
/// one of its methods, so invariants cannot be bypassed from the outside.
/// </summary>
public abstract class AggregateRoot<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];

    protected AggregateRoot(TId id) => Id = id;

    public TId Id { get; }

    /// <summary>Monotonically increasing; used for optimistic concurrency.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    protected void Raise(IDomainEvent domainEvent)
    {
        _domainEvents.Add(domainEvent);
        Version++;
    }

    /// <summary>Drains pending events so an adapter can publish them. </summary>
    public IReadOnlyList<IDomainEvent> DequeueEvents()
    {
        var drained = _domainEvents.ToArray();
        _domainEvents.Clear();
        return drained;
    }
}
