using AgentForge.Domain.Abstractions;

namespace AgentForge.Domain.Delivery;

/// <summary>
/// The ceiling. Immutable policy attached to a task when it starts.
/// Budget is modelled in the domain on purpose: running out of money is a
/// business outcome (escalate to a human), not an infrastructure error.
/// </summary>
public readonly record struct Budget(int MaxSteps, long MaxTokens, Money MaxCost, int MaxReworkLoops)
{
    public static Budget Default => new(
        MaxSteps: 12,
        MaxTokens: 200_000,
        MaxCost: Money.Usd(2.00m),
        MaxReworkLoops: 2);

    private readonly int _maxSteps = Ensure.Positive(MaxSteps, nameof(MaxSteps));
    private readonly long _maxTokens = Ensure.Positive(MaxTokens, nameof(MaxTokens));
    private readonly Money _maxCost = PositiveCost(MaxCost);
    private readonly int _maxReworkLoops = Ensure.NonNegative(MaxReworkLoops, nameof(MaxReworkLoops));

    // Validation lives in the init accessors rather than in the primary constructor
    // so that `budget with { MaxSteps = 0 }` is rejected too. A guard that only
    // runs on construction is a guard with a hole in it.
    public int MaxSteps
    {
        get => _maxSteps;
        init => _maxSteps = Ensure.Positive(value, nameof(MaxSteps));
    }

    public long MaxTokens
    {
        get => _maxTokens;
        init => _maxTokens = Ensure.Positive(value, nameof(MaxTokens));
    }

    public Money MaxCost
    {
        get => _maxCost;
        init => _maxCost = PositiveCost(value);
    }

    public int MaxReworkLoops
    {
        get => _maxReworkLoops;
        init => _maxReworkLoops = Ensure.NonNegative(value, nameof(MaxReworkLoops));
    }

    private static Money PositiveCost(Money value) => value.Amount > 0
        ? value
        : throw new DomainException($"{nameof(MaxCost)} must be positive, was {value}.");
}

/// <summary>What has actually been spent so far.</summary>
public readonly record struct Consumption(int Steps, TokenUsage Tokens, Money Cost)
{
    public static readonly Consumption Zero = new(0, TokenUsage.None, Money.Zero);

    public Consumption Add(TokenUsage tokens, Money cost) =>
        new(Steps + 1, Tokens + tokens, Cost + cost);

    /// <summary>The single place that decides whether a task may keep spending.</summary>
    public BudgetVerdict Against(Budget budget) => this switch
    {
        _ when Steps >= budget.MaxSteps => BudgetVerdict.StepsExhausted,
        _ when Tokens.Total >= budget.MaxTokens => BudgetVerdict.TokensExhausted,
        _ when Cost.IsGreaterThan(budget.MaxCost) => BudgetVerdict.CostExhausted,
        _ => BudgetVerdict.WithinBudget,
    };
}

public enum BudgetVerdict
{
    WithinBudget,
    StepsExhausted,
    TokensExhausted,
    CostExhausted,
}
