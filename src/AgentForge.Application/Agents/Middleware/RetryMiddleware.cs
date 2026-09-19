using AgentForge.Domain.Delivery;
using Microsoft.Extensions.Logging;

namespace AgentForge.Application.Agents.Middleware;

/// <summary>
/// Retries a transiently failed step with exponential backoff.
///
/// It sits inside the budget guard on purpose: every attempt is charged, so a
/// retry storm is capped by the same ceiling as a single call.
/// </summary>
public sealed class RetryMiddleware(ILogger<RetryMiddleware> logger, TimeProvider time, int maxAttempts = 3) : IAgentMiddleware
{
    public string Name => "retry";

    public async Task<AgentResponse> InvokeAsync(AgentRequest request, AgentDelegate next, CancellationToken cancellationToken)
    {
        AgentResponse response = AgentResponse.Failed("not attempted");

        // Attempts that failed still burned tokens. They are carried forward so
        // the guard above and the task budget see what a retry storm really cost;
        // dropping them would make the cheapest-looking run the most expensive one.
        var spentTokens = TokenUsage.None;
        var spentCost = Money.Zero;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            response = await next(request, cancellationToken).ConfigureAwait(false);
            spentTokens += response.Tokens;
            spentCost += response.Cost;

            if (response.Outcome is not AgentOutcome.Failed)
            {
                return response with { Tokens = spentTokens, Cost = spentCost };
            }

            if (attempt == maxAttempts)
            {
                break;
            }

            var backoff = TimeSpan.FromMilliseconds(100 * Math.Pow(2, attempt - 1));
            logger.LogWarning(
                "step {Step} failed ({Reason}); retrying in {Backoff}ms (attempt {Attempt}/{Max})",
                request.Step, response.FailureReason, backoff.TotalMilliseconds, attempt + 1, maxAttempts);

            await Task.Delay(backoff, time, cancellationToken).ConfigureAwait(false);
        }

        return response with { Tokens = spentTokens, Cost = spentCost };
    }
}
