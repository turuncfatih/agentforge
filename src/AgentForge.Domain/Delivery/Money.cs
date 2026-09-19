namespace AgentForge.Domain.Delivery;

/// <summary>Cost in USD. A domain concept here, because budget is a domain rule.</summary>
public readonly record struct Money(decimal Amount)
{
    public static readonly Money Zero = new(0m);

    public static Money Usd(decimal amount) => new(amount);

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);

    public bool IsGreaterThan(Money other) => Amount > other.Amount;

    public override string ToString() => $"${Amount:0.####}";
}
