using AgentForge.Domain.Abstractions;
using AgentForge.Domain.Delivery;

namespace AgentForge.Domain.Tests;

/// <summary>
/// Each test names one invariant. If a test here fails, a delivery rule broke —
/// not a wiring detail, and not a prompt.
/// </summary>
public sealed class DeliveryTaskTests
{
    [Fact]
    public void A_step_settles_exactly_once_so_a_replay_cannot_double_charge()
    {
        var task = Any.Planned();
        var step = task.OpenStep(AgentId.Backend, Any.Now);
        task.SettleStep(step, AgentOutcome.Completed, [], [], new TokenUsage(10, 10), Money.Usd(0.01m), Any.Now);

        var replay = () => task.SettleStep(step, AgentOutcome.Completed, [], [], new TokenUsage(10, 10), Money.Usd(0.01m), Any.Now);

        var error = Assert.Throws<DomainException>(replay);
        Assert.Contains("already settled", error.Message, StringComparison.Ordinal);
        Assert.Equal(1, task.Consumption.Steps);
    }

    [Fact]
    public void Step_ids_are_deterministic_so_the_same_work_resolves_to_the_same_step()
    {
        var task = Any.Planned();
        var id = task.OpenStep(AgentId.Backend, Any.Now);

        Assert.Equal(StepId.For(task.Id, AgentId.Backend, round: 0), id);
    }

    [Fact]
    public void No_step_may_open_once_the_step_budget_is_spent()
    {
        var budget = Budget.Default with { MaxSteps = 1 };
        var task = Any.Planned(budget);

        var first = task.OpenStep(AgentId.Backend, Any.Now);
        task.SettleStep(first, AgentOutcome.Completed, [], [], new TokenUsage(10, 10), Money.Usd(0.01m), Any.Now);

        var overspend = () => { _ = task.OpenStep(AgentId.Tester, Any.Now); };

        Assert.Contains("Budget exhausted", Assert.Throws<DomainException>(overspend).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_blocking_finding_cannot_be_approved_away()
    {
        var task = Any.Planned();
        task.RunRound(Any.Blocking());

        var decision = task.RunGate(SeverityThresholdGate.BlockOnCritical, Any.Now);

        Assert.Equal(GateDecision.Blocked, decision);
        Assert.Equal(DeliveryState.Reworking, task.State);
        Assert.NotEqual(DeliveryState.Approved, task.State);
    }

    [Fact]
    public void A_clean_review_approves_the_delivery()
    {
        var task = Any.Planned();
        task.RunRound(Any.Minor());

        var decision = task.RunGate(SeverityThresholdGate.BlockOnCritical, Any.Now);

        Assert.Equal(GateDecision.Passed, decision);
        Assert.Equal(DeliveryState.Approved, task.State);
    }

    [Fact]
    public void The_gate_cannot_run_before_every_worker_has_settled()
    {
        var task = Any.Planned();
        var step = task.OpenStep(AgentId.Backend, Any.Now);
        task.SettleStep(step, AgentOutcome.Completed, [], [], new TokenUsage(10, 10), Money.Usd(0.01m), Any.Now);

        var premature = () => { _ = task.RunGate(SeverityThresholdGate.BlockOnCritical, Any.Now); };

        Assert.Contains("every worker", Assert.Throws<DomainException>(premature).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Rework_is_bounded_and_a_task_that_keeps_failing_goes_to_a_human()
    {
        var budget = Budget.Default with { MaxReworkLoops = 1 };
        var task = Any.Planned(budget);

        task.RunRound(Any.Blocking());
        task.RunGate(SeverityThresholdGate.BlockOnCritical, Any.Now);
        Assert.Equal(DeliveryState.Reworking, task.State);

        task.BeginRework("still blocked", Any.Now);
        task.RunRound(Any.Blocking());
        task.RunGate(SeverityThresholdGate.BlockOnCritical, Any.Now);

        Assert.Equal(DeliveryState.Escalated, task.State);
        Assert.Contains("rework", task.TerminalReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_report_cannot_be_published_before_the_gate_approves()
    {
        var task = Any.Planned();

        var premature = () => task.Report(Any.Now);

        Assert.Contains("expected state Approved", Assert.Throws<DomainException>(premature).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_transition_is_recorded_so_the_decision_can_be_explained_later()
    {
        var task = Any.Planned();
        task.RunRound(Any.Minor());
        task.RunGate(SeverityThresholdGate.BlockOnCritical, Any.Now);
        task.Report(Any.Now);

        var trail = task.DomainEvents.Select(e => e.GetType().Name).ToArray();

        Assert.Equal("DeliveryStarted", trail[0]);
        Assert.Contains("GateEvaluated", trail);
        Assert.Contains("DeliveryApproved", trail);
        Assert.Equal("DeliveryReported", trail[^1]);
    }
}
