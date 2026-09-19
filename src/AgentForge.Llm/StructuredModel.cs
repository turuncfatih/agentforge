using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AgentForge.Llm;

/// <summary>
/// Turns an <see cref="IChatClient"/> into a typed call.
///
/// Two things earn their place here:
///   - a single repair round-trip, because a malformed first answer is common
///     and re-running the whole step to fix a missing brace is wasteful;
///   - usage accounting at the only point that actually knows the token counts.
/// </summary>
public sealed class StructuredModel(IChatClient client, ModelPricing pricing) : IStructuredModel
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ModelId => pricing.ModelId;

    public async Task<LlmResult<T>> CompleteAsync<T>(
        string systemPrompt,
        string userPrompt,
        CancellationToken cancellationToken)
        where T : class
    {
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, systemPrompt),
            new(ChatRole.User, userPrompt),
        };

        var (text, usage) = await SendAsync(messages, cancellationToken).ConfigureAwait(false);

        if (TryParse<T>(text, out var value, out var error))
        {
            return new LlmResult<T>(value!, usage);
        }

        // One repair attempt, with the parser's own complaint as the feedback.
        messages.Add(new ChatMessage(ChatRole.Assistant, text));
        messages.Add(new ChatMessage(
            ChatRole.User,
            $"That response could not be parsed: {error}. Reply again with JSON only, no prose and no code fences."));

        var (repaired, repairUsage) = await SendAsync(messages, cancellationToken).ConfigureAwait(false);
        usage += repairUsage;

        if (TryParse<T>(repaired, out var repairedValue, out var repairError))
        {
            return new LlmResult<T>(repairedValue!, usage);
        }

        throw new StructuredOutputException(
            $"{ModelId} did not return valid {typeof(T).Name} after a repair attempt: {repairError}");
    }

    private async Task<(string Text, LlmUsage Usage)> SendAsync(
        IReadOnlyList<ChatMessage> messages,
        CancellationToken cancellationToken)
    {
        var response = await client.GetResponseAsync(messages, options: null, cancellationToken).ConfigureAwait(false);

        var input = response.Usage?.InputTokenCount ?? 0;
        var output = response.Usage?.OutputTokenCount ?? 0;
        var usage = new LlmUsage(input, output, pricing.CostOf(input, output));

        return (response.Text, usage);
    }

    private static bool TryParse<T>(string raw, out T? value, out string? error)
        where T : class
    {
        try
        {
            value = JsonSerializer.Deserialize<T>(Strip(raw), Json);
            error = value is null ? "deserialised to null" : null;
            return value is not null;
        }
        catch (JsonException ex)
        {
            value = null;
            error = ex.Message;
            return false;
        }
    }

    /// <summary>Models like to wrap JSON in code fences however firmly you ask them not to.</summary>
    private static string Strip(string raw)
    {
        var text = raw.Trim();
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstNewline = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline < 0 || lastFence <= firstNewline
            ? text
            : text[(firstNewline + 1)..lastFence].Trim();
    }
}
