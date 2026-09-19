namespace AgentForge.Domain.Abstractions;

/// <summary>
/// Raised when an operation would break a domain invariant.
/// This is a bug in the caller, never an expected control-flow path.
/// </summary>
public sealed class DomainException(string message) : Exception(message);

public static class Ensure
{
    public static void That(bool condition, string message)
    {
        if (!condition)
        {
            throw new DomainException(message);
        }
    }

    public static string NotBlank(string? value, string name)
    {
        That(!string.IsNullOrWhiteSpace(value), $"{name} must not be blank.");
        return value!.Trim();
    }

    public static int Positive(int value, string name)
    {
        That(value > 0, $"{name} must be positive, was {value}.");
        return value;
    }

    public static long Positive(long value, string name)
    {
        That(value > 0, $"{name} must be positive, was {value}.");
        return value;
    }

    public static int NonNegative(int value, string name)
    {
        That(value >= 0, $"{name} cannot be negative, was {value}.");
        return value;
    }
}
