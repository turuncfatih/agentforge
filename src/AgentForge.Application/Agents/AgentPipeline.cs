namespace AgentForge.Application.Agents;

public delegate Task<AgentResponse> AgentDelegate(AgentRequest request, CancellationToken cancellationToken);

/// <summary>
/// One concern, wrapped around an agent call. Deliberately shaped like ASP.NET
/// middleware: cross-cutting rules belong in the pipeline, not inside prompts.
/// </summary>
public interface IAgentMiddleware
{
    string Name { get; }

    Task<AgentResponse> InvokeAsync(AgentRequest request, AgentDelegate next, CancellationToken cancellationToken);
}

/// <summary>
/// Composes middleware around every agent invocation. Order is meaningful and
/// is asserted by a test, because "retry outside the budget guard" and
/// "retry inside the budget guard" are very different systems.
/// </summary>
public sealed class AgentPipeline(IReadOnlyList<IAgentMiddleware> middleware)
{
    public IReadOnlyList<string> Stages => [.. middleware.Select(m => m.Name)];

    public Task<AgentResponse> ExecuteAsync(IAgent agent, AgentRequest request, CancellationToken cancellationToken)
    {
        AgentDelegate pipeline = agent.ExecuteAsync;

        for (var i = middleware.Count - 1; i >= 0; i--)
        {
            var stage = middleware[i];
            var next = pipeline;
            pipeline = (req, ct) => stage.InvokeAsync(req, next, ct);
        }

        return pipeline(request, cancellationToken);
    }
}
