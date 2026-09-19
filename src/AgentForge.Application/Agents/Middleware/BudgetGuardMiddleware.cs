using AgentForge.Domain.Delivery;
using Microsoft.Extensions.Logging;

namespace AgentForge.Application.Agents.Middleware;

/// <summary>
/// Enforces the per-step token ceiling declared in the agent's policy.
/// The aggregate guards the task-level budget; this guards a single runaway
/// step, which is the failure mode that actually produces surprise invoices.
/// </summary>
public sealed class BudgetGuardMiddleware(ILogger<BudgetGuardMiddleware> logger) : IAgentMiddleware
{
    public string Name => "budget-guard";

    public async Task<AgentResponse> InvokeAsync(AgentRequest request, AgentDelegate next, CancellationToken cancellationToken)
    {
        var response = await next(request, cancellationToken).ConfigureAwait(false);

        if (response.Tokens.Total <= request.Policy.MaxTokensPerStep)
        {
            return response;
        }

        logger.LogWarning(
            "step {Step} used {Used} tokens, over its policy ceiling of {Ceiling}; failing the step",
            request.Step, response.Tokens.Total, request.Policy.MaxTokensPerStep);

        // The spend still happened, so it is reported honestly rather than zeroed out.
        return response with
        {
            Outcome = AgentOutcome.Failed,
            Artifacts = [],
            Findings = [],
            FailureReason = $"per-step token ceiling exceeded: {response.Tokens.Total} > {request.Policy.MaxTokensPerStep}",
        };
    }
}
