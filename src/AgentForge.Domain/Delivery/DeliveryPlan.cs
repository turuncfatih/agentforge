using AgentForge.Domain.Abstractions;

namespace AgentForge.Domain.Delivery;

/// <summary>
/// Which agents run, and in what shape. Produced by the orchestrator,
/// validated by the aggregate before it is accepted.
/// </summary>
public sealed record DeliveryPlan
{
    public DeliveryPlan(IReadOnlyList<AgentId> parallelWorkers, AgentId gate, AgentId reporter)
    {
        Ensure.That(parallelWorkers.Count > 0, "A plan needs at least one worker.");
        Ensure.That(!parallelWorkers.Contains(gate), "The gate agent cannot also be a worker: it must review work it did not produce.");
        Ensure.That(parallelWorkers.Distinct().Count() == parallelWorkers.Count, "An agent cannot appear twice in the same round.");

        ParallelWorkers = parallelWorkers;
        Gate = gate;
        Reporter = reporter;
    }

    public IReadOnlyList<AgentId> ParallelWorkers { get; }
    public AgentId Gate { get; }
    public AgentId Reporter { get; }
}
