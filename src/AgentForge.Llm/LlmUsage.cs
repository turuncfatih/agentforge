namespace AgentForge.Llm;

/// <summary>
/// What a model call consumed. Note this type is not the domain's TokenUsage:
/// the LLM layer has no reference to the domain, and the agents layer maps
/// between the two. The boundary is kept honest by the dependency graph itself.
/// </summary>
public readonly record struct LlmUsage(long InputTokens, long OutputTokens, decimal CostUsd)
{
    public static readonly LlmUsage None = new(0, 0, 0m);

    public long TotalTokens => InputTokens + OutputTokens;

    public static LlmUsage operator +(LlmUsage a, LlmUsage b) =>
        new(a.InputTokens + b.InputTokens, a.OutputTokens + b.OutputTokens, a.CostUsd + b.CostUsd);
}

/// <summary>Per-million-token prices, so cost is computed where the tokens are counted.</summary>
public sealed record ModelPricing(string ModelId, decimal InputPerMillion, decimal OutputPerMillion)
{
    public static readonly ModelPricing Free = new("fake", 0m, 0m);

    public decimal CostOf(long inputTokens, long outputTokens) =>
        (inputTokens * InputPerMillion / 1_000_000m) + (outputTokens * OutputPerMillion / 1_000_000m);
}

public sealed record LlmResult<T>(T Value, LlmUsage Usage);

/// <summary>Thrown when the model could not be coaxed into the requested shape.</summary>
public sealed class StructuredOutputException(string message) : Exception(message);
