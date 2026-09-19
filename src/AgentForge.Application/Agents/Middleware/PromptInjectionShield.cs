using System.Text.RegularExpressions;
using AgentForge.Domain.Delivery;
using Microsoft.Extensions.Logging;

namespace AgentForge.Application.Agents.Middleware;

/// <summary>
/// The feature request is written by a human and is the only untrusted text in
/// the system. It is data, never instructions, so override attempts are
/// neutralised before any agent sees them.
///
/// This is a defence in depth layer, not a solved problem: it lowers the blast
/// radius of an injected request, it does not make injection impossible.
/// </summary>
public sealed partial class PromptInjectionShield(ILogger<PromptInjectionShield> logger) : IAgentMiddleware
{
    private const string Redaction = "[redacted: instruction-like text in untrusted input]";

    public string Name => "injection-shield";

    public Task<AgentResponse> InvokeAsync(AgentRequest request, AgentDelegate next, CancellationToken cancellationToken)
    {
        var original = request.Context.Request;
        var sanitised = Sanitise(original.Description, out var hits);

        if (hits == 0)
        {
            return next(request, cancellationToken);
        }

        logger.LogWarning(
            "neutralised {Hits} instruction-like pattern(s) in the feature request for task {Task}",
            hits, request.Task);

        var safeRequest = new FeatureRequest(original.Title, sanitised, original.AcceptanceCriteria);
        var safeContext = request.Context with { Request = safeRequest };
        return next(request with { Context = safeContext }, cancellationToken);
    }

    internal static string Sanitise(string input, out int hits)
    {
        var result = OverrideAttempt().Replace(input, Redaction);
        hits = OverrideAttempt().Count(input);
        return result;
    }

    [GeneratedRegex(
        @"(?im)^\s*(system|assistant|developer)\s*:|ignore\s+(all\s+)?(previous|prior|above)\s+instructions|disregard\s+(the\s+)?(above|previous)|you\s+are\s+now\s+|new\s+instructions\s*:",
        RegexOptions.CultureInvariant)]
    private static partial Regex OverrideAttempt();
}
