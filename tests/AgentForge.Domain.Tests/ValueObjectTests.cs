using AgentForge.Domain.Abstractions;
using AgentForge.Domain.Delivery;

namespace AgentForge.Domain.Tests;

public sealed class ValueObjectTests
{
    [Fact]
    public void A_finding_without_evidence_is_rejected_because_the_gate_may_not_block_on_an_opinion()
    {
        var unevidenced = () => { _ = new Finding(AgentId.Security, Severity.Critical, "authorization", "looks risky", evidence: "  "); };

        Assert.Throws<DomainException>(unevidenced);
    }

    [Fact]
    public void The_gate_agent_may_not_also_be_a_worker()
    {
        var selfReview = () => { _ = new DeliveryPlan(
            parallelWorkers: [AgentId.Backend, AgentId.Security],
            gate: AgentId.Security,
            reporter: AgentId.Analyst); };

        Assert.Contains("review work it did not produce", Assert.Throws<DomainException>(selfReview).Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(-1, 10, 1)]
    public void A_budget_must_be_spendable(int maxSteps, long maxTokens, int reworkLoops)
    {
        var nonsense = () => { _ = new Budget(maxSteps, maxTokens, Money.Usd(1m), reworkLoops); };

        Assert.Throws<DomainException>(nonsense);
    }

    [Fact]
    public void Consumption_reports_which_ceiling_was_hit()
    {
        var budget = new Budget(MaxSteps: 5, MaxTokens: 1_000, MaxCost: Money.Usd(1m), MaxReworkLoops: 2);
        var spent = new Consumption(Steps: 2, Tokens: new TokenUsage(600, 500), Cost: Money.Usd(0.10m));

        Assert.Equal(BudgetVerdict.TokensExhausted, spent.Against(budget));
    }
}
