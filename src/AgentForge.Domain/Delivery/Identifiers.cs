using AgentForge.Domain.Abstractions;

namespace AgentForge.Domain.Delivery;

public readonly record struct TaskId(Guid Value)
{
    public static TaskId New() => new(Guid.CreateVersion7());

    /// <summary>Short form, for logs and step ids. Use <see cref="Value"/> on the wire.</summary>
    public override string ToString() => Value.ToString("N")[..8];
}

/// <summary>Stable name of a role, not of a model or a vendor.</summary>
public readonly record struct AgentId(string Value)
{
    public static readonly AgentId Orchestrator = new("orchestrator");
    public static readonly AgentId Backend = new("backend");
    public static readonly AgentId Security = new("security");
    public static readonly AgentId Tester = new("tester");
    public static readonly AgentId Analyst = new("analyst");

    public override string ToString() => Value;
}

/// <summary>
/// Deterministic by construction: the same task, agent and round always produce
/// the same id. That is what makes a step replayable and billed exactly once.
/// </summary>
public readonly record struct StepId(string Value)
{
    public static StepId For(TaskId task, AgentId agent, int round) =>
        new($"{task}:{agent}:r{round}");

    public override string ToString() => Value;
}
