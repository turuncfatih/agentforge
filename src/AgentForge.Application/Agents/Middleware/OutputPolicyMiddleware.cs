using AgentForge.Domain.Delivery;
using Microsoft.Extensions.Logging;

namespace AgentForge.Application.Agents.Middleware;

/// <summary>
/// Validates an agent's output against its own policy.
///
/// The rule that matters: only an agent granted veto power may raise a
/// blocking finding. Without this, any agent could halt delivery just by
/// labelling its opinion "Critical" — capability would live in the model's
/// output instead of in configuration.
/// </summary>
public sealed class OutputPolicyMiddleware(ILogger<OutputPolicyMiddleware> logger) : IAgentMiddleware
{
    public string Name => "output-policy";

    public async Task<AgentResponse> InvokeAsync(AgentRequest request, AgentDelegate next, CancellationToken cancellationToken)
    {
        var response = await next(request, cancellationToken).ConfigureAwait(false);

        if (request.Policy.CanVeto || response.Findings.Count == 0)
        {
            return response;
        }

        var overreaching = response.Findings.Where(f => f.IsBlocking).ToArray();
        if (overreaching.Length == 0)
        {
            return response;
        }

        logger.LogWarning(
            "agent {Agent} has no veto power but raised {Count} blocking finding(s); downgrading them to High",
            request.Agent, overreaching.Length);

        var corrected = response.Findings
            .Select(f => f.IsBlocking
                ? new Finding(f.RaisedBy, Severity.High, f.Category, f.Title, f.Evidence, f.SuggestedFix)
                : f)
            .ToArray();

        return response with { Findings = corrected };
    }
}
