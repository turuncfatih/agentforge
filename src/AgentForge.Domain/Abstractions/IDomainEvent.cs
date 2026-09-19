namespace AgentForge.Domain.Abstractions;

/// <summary>
/// A fact that has already happened inside the domain. Named in past tense.
/// Domain events never carry infrastructure types.
/// </summary>
public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}
