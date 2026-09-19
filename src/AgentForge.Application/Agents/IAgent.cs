using AgentForge.Domain.Delivery;

namespace AgentForge.Application.Agents;

public sealed record AgentRequest(
    TaskId Task,
    StepId Step,
    AgentId Agent,
    string Goal,
    Blackboard Context,
    AgentPolicy Policy);

public sealed record AgentResponse(
    AgentOutcome Outcome,
    IReadOnlyList<Artifact> Artifacts,
    IReadOnlyList<Finding> Findings,
    TokenUsage Tokens,
    Money Cost,
    string? FailureReason = null)
{
    public static AgentResponse Failed(string reason) =>
        new(AgentOutcome.Failed, [], [], TokenUsage.None, Money.Zero, reason);
}

/// <summary>
/// A role, not a model. Swapping the model behind an agent must not change
/// this contract, which is why nothing here mentions prompts or providers.
/// </summary>
public interface IAgent
{
    AgentId Id { get; }

    AgentPolicy Policy { get; }

    Task<AgentResponse> ExecuteAsync(AgentRequest request, CancellationToken cancellationToken);
}
