using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace AgentForge.Application.Agents.Middleware;

/// <summary>
/// One span per agent step, tagged with the cost. Token spend is an
/// operational metric here, not an afterthought in a log line.
/// </summary>
public sealed class TelemetryMiddleware(ILogger<TelemetryMiddleware> logger) : IAgentMiddleware
{
    public static readonly ActivitySource Source = new("AgentForge.Agents");

    public string Name => "telemetry";

    public async Task<AgentResponse> InvokeAsync(AgentRequest request, AgentDelegate next, CancellationToken cancellationToken)
    {
        using var activity = Source.StartActivity($"agent {request.Agent}", ActivityKind.Internal);
        activity?.SetTag("agentforge.task", request.Task.ToString());
        activity?.SetTag("agentforge.step", request.Step.ToString());
        activity?.SetTag("agentforge.agent", request.Agent.Value);
        activity?.SetTag("agentforge.round", request.Context.Round);

        var started = Stopwatch.GetTimestamp();
        var response = await next(request, cancellationToken).ConfigureAwait(false);
        var elapsed = Stopwatch.GetElapsedTime(started);

        activity?.SetTag("agentforge.outcome", response.Outcome.ToString());
        activity?.SetTag("agentforge.tokens", response.Tokens.Total);
        activity?.SetTag("agentforge.cost_usd", response.Cost.Amount);

        logger.LogInformation(
            "step {Step} agent={Agent} outcome={Outcome} tokens={Tokens} cost={Cost} in {Elapsed}ms",
            request.Step, request.Agent, response.Outcome, response.Tokens.Total, response.Cost, elapsed.TotalMilliseconds);

        return response;
    }
}
