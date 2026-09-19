using AgentForge.Domain.Delivery;

namespace AgentForge.Domain.Tests;

internal static class Any
{
    public static readonly DateTimeOffset Now = new(2026, 9, 19, 12, 0, 0, TimeSpan.Zero);

    public static readonly DeliveryPlan Plan = new(
        parallelWorkers: [AgentId.Backend, AgentId.Tester],
        gate: AgentId.Security,
        reporter: AgentId.Analyst);

    public static FeatureRequest Request() =>
        new("Cancel an order", "A customer cancels an order that has not shipped.");

    public static DeliveryTask Task(Budget? budget = null) =>
        DeliveryTask.Start(TaskId.New(), Request(), budget ?? Budget.Default, Now);

    public static DeliveryTask Planned(Budget? budget = null)
    {
        var task = Task(budget);
        task.AcceptPlan(Plan, Now);
        return task;
    }

    public static Finding Blocking(string title = "missing authorization check") =>
        new(AgentId.Security, Severity.Critical, "authorization", title, "the design never checks the caller against Order.CustomerId");

    public static Finding Minor(string title = "free-text reason") =>
        new(AgentId.Security, Severity.Low, "observability", title, "CancellationReason has no controlled vocabulary");

    public static Artifact Design() =>
        new(AgentId.Backend, "design", "Cancellation design", "POST /orders/{id}/cancel");

    /// <summary>Runs one full round: both workers settle, then the gate agent settles.</summary>
    public static void RunRound(this DeliveryTask task, params Finding[] gateFindings)
    {
        foreach (var worker in Plan.ParallelWorkers)
        {
            var step = task.OpenStep(worker, Now);
            task.SettleStep(step, AgentOutcome.Completed, [Design()], [], new TokenUsage(100, 100), Money.Usd(0.01m), Now);
        }

        var gateStep = task.OpenStep(Plan.Gate, Now);
        task.SettleStep(gateStep, AgentOutcome.Completed, [], gateFindings, new TokenUsage(100, 100), Money.Usd(0.01m), Now);
    }
}
