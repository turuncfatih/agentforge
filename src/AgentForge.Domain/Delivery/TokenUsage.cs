namespace AgentForge.Domain.Delivery;

/// <summary>
/// How much a step consumed. The domain knows the number, never who produced it.
/// </summary>
public readonly record struct TokenUsage(long Input, long Output)
{
    public static readonly TokenUsage None = new(0, 0);

    public long Total => Input + Output;

    public static TokenUsage operator +(TokenUsage a, TokenUsage b) =>
        new(a.Input + b.Input, a.Output + b.Output);
}
