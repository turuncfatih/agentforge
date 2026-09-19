using AgentForge.Application.Agents;
using AgentForge.Domain.Delivery;
using AgentForge.Llm;

namespace AgentForge.Agents;

/// <summary>
/// The fifth role. It does one thing an LLM is genuinely good at — deciding
/// which specialists a request needs — and nothing it is bad at: it does not
/// sequence the run, enforce the budget or judge the gate. Those live in
/// <c>DeliveryOrchestrator</c> and in the aggregate, where they are testable.
/// </summary>
public sealed class OrchestratorAgent(IStructuredModel model) : LlmAgent(model)
{
    public const string PlanArtifactKind = "plan";

    public override AgentId Id => AgentId.Orchestrator;

    /// <summary>No tools at all: the orchestrator may only think, never act.</summary>
    public override AgentPolicy Policy => AgentPolicy.ReadOnly(maxTokens: 8_000);

    protected override string SystemPrompt =>
        $$"""
        You are the delivery orchestrator. Choose which specialists should work on
        this request, and nothing else.

        Available roles:
          backend  - designs the change
          tester   - derives acceptance criteria
          security - reviews for risk; the only role that may block delivery
          analyst  - writes the closing report

        Emit exactly one artifact with kind "{{PlanArtifactKind}}" whose body is JSON:
        {"workers":["backend","tester"],"gate":"security","reporter":"analyst"}

        Constraints: the gate must not also be a worker, and a role may appear at
        most once among the workers. Raise no findings.
        """;
}
