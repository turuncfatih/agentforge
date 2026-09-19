using AgentForge.Agents.Demo;
using AgentForge.Domain.Delivery;
using AgentForge.Llm.Fake;

namespace AgentForge.Application.Tests;

public sealed class DeliveryPipelineTests
{
    [Fact]
    public async Task A_blocked_review_triggers_rework_and_the_second_pass_ships()
    {
        var harness = new Harness(DemoScript.OrderCancellation());

        var task = await harness.RunAsync(TaskId.New());

        Assert.Equal(DeliveryState.Reported, task.State);
        Assert.Equal(1, task.Round);

        // Round 0 was blocked on a Critical finding; round 1 resolved it.
        Assert.Contains(task.AllFindings, f => f.Severity is Severity.Critical);
        Assert.Contains(task.Steps, s => s.Agent == AgentId.Backend && s.Round == 1);
        Assert.Contains(task.AllArtifacts, a => a.Title.Contains("revised", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Every_step_is_accounted_for_so_the_bill_can_be_explained()
    {
        var harness = new Harness(DemoScript.OrderCancellation());

        var task = await harness.RunAsync(TaskId.New());

        Assert.Equal(task.Steps.Count, task.Consumption.Steps);
        Assert.Equal(task.Steps.Sum(s => s.Tokens.Total), task.Consumption.Tokens.Total);
        Assert.All(task.Steps, s => Assert.Equal(StepStatus.Settled, s.Status));
    }

    [Fact]
    public async Task Rerunning_a_finished_delivery_repeats_no_work()
    {
        var harness = new Harness(DemoScript.OrderCancellation());
        var id = TaskId.New();

        var first = await harness.RunAsync(id);
        var callsAfterFirstRun = harness.Client.CallCount;

        var second = await harness.RunAsync(id);

        Assert.Same(first, second);
        Assert.Equal(callsAfterFirstRun, harness.Client.CallCount);
        Assert.Equal(first.Consumption.Steps, second.Consumption.Steps);
    }

    [Fact]
    public async Task A_delivery_that_never_satisfies_the_gate_escalates_instead_of_looping_forever()
    {
        // The security agent blocks on every single review.
        var stubborn = ScriptedChatClient.Create()
            .WhenPromptContains("You are the security reviewer",
                """
                {"summary":"still unsafe","artifacts":[],
                 "findings":[{"severity":"Critical","category":"authorization","title":"ownership still unchecked",
                   "evidence":"the revised design still omits the caller check"}]}
                """)
            .WhenPromptContains("You are the delivery orchestrator",
                """
                {"summary":"plan","artifacts":[{"kind":"plan","title":"Plan",
                  "body":"{\"workers\":[\"backend\",\"tester\"],\"gate\":\"security\",\"reporter\":\"analyst\"}"}],"findings":[]}
                """)
            .Otherwise("""{"summary":"ok","artifacts":[{"kind":"design","title":"design","body":"unchanged"}],"findings":[]}""");

        var harness = new Harness(stubborn, Budget.Default with { MaxReworkLoops = 1 });

        var task = await harness.RunAsync(TaskId.New());

        Assert.Equal(DeliveryState.Escalated, task.State);
        Assert.Contains("rework", task.TerminalReason!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_delivery_that_runs_out_of_budget_escalates_rather_than_overspending()
    {
        var harness = new Harness(DemoScript.OrderCancellation(), Budget.Default with { MaxSteps = 3 });

        var task = await harness.RunAsync(TaskId.New());

        Assert.Equal(DeliveryState.Escalated, task.State);
        Assert.Contains("budget exhausted", task.TerminalReason!, StringComparison.OrdinalIgnoreCase);
        Assert.True(task.Consumption.Steps <= 3);
    }

    [Fact]
    public async Task The_audit_trail_explains_the_decision_without_reading_any_code()
    {
        var harness = new Harness(DemoScript.OrderCancellation());
        var id = TaskId.New();

        await harness.RunAsync(id);

        var trail = harness.Events.Read(id).Select(e => e.Type).ToArray();

        Assert.Equal("DeliveryStarted", trail[0]);
        Assert.Contains("GateEvaluated", trail);
        Assert.Contains("ReworkRequested", trail);
        Assert.Equal("DeliveryReported", trail[^1]);
    }
}
