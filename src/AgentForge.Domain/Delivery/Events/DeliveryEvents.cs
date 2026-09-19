using AgentForge.Domain.Abstractions;

namespace AgentForge.Domain.Delivery.Events;

public sealed record DeliveryStarted(TaskId Task, string Title, Budget Budget, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record PlanAccepted(TaskId Task, DeliveryPlan Plan, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record StepOpened(TaskId Task, StepId Step, AgentId Agent, int Round, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record StepSettled(TaskId Task, StepId Step, AgentOutcome Outcome, TokenUsage Tokens, Money Cost, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record GateEvaluated(TaskId Task, int Round, GateDecision Decision, int BlockingFindings, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ReworkRequested(TaskId Task, int Round, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DeliveryApproved(TaskId Task, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DeliveryReported(TaskId Task, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DeliveryEscalated(TaskId Task, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record DeliveryFailed(TaskId Task, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
