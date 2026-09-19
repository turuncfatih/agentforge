using AgentForge.Agents;
using AgentForge.Application.Agents;
using AgentForge.Application.Agents.Middleware;
using AgentForge.Application.Orchestration;
using AgentForge.Application.Ports;
using AgentForge.Domain.Delivery;
using AgentForge.Infrastructure.Observability;
using AgentForge.Infrastructure.Persistence;
using AgentForge.Llm;
using AgentForge.Llm.Fake;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentForge.Application.Tests;

/// <summary>
/// The whole system, assembled in-process against a scripted model.
/// No API key, no network, no cost, same result every run.
/// </summary>
internal sealed class Harness
{
    public Harness(ScriptedChatClient client, Budget? budget = null, IGatePolicy? gate = null)
    {
        Client = client;
        Budget = budget ?? Budget.Default;

        var model = new StructuredModel(client, ModelPricing.Free);
        Registry = new AgentRegistry(
        [
            new OrchestratorAgent(model),
            new BackendAgent(model),
            new TesterAgent(model),
            new SecurityAgent(model),
            new AnalystAgent(model),
        ]);

        Pipeline = new AgentPipeline(
        [
            new TelemetryMiddleware(NullLogger<TelemetryMiddleware>.Instance),
            new PromptInjectionShield(NullLogger<PromptInjectionShield>.Instance),
            new BudgetGuardMiddleware(NullLogger<BudgetGuardMiddleware>.Instance),
            new RetryMiddleware(NullLogger<RetryMiddleware>.Instance, TimeProvider.System),
            new OutputPolicyMiddleware(NullLogger<OutputPolicyMiddleware>.Instance),
        ]);

        Orchestrator = new DeliveryOrchestrator(
            Registry,
            Pipeline,
            gate ?? SeverityThresholdGate.BlockOnCritical,
            new PlanReader(Registry, NullLogger<PlanReader>.Instance),
            Repository,
            Events,
            TimeProvider.System,
            NullLogger<DeliveryOrchestrator>.Instance);
    }

    public ScriptedChatClient Client { get; }

    public Budget Budget { get; }

    public AgentRegistry Registry { get; }

    public AgentPipeline Pipeline { get; }

    public InMemoryDeliveryTaskRepository Repository { get; } = new();

    public DeliveryEventLog Events { get; } = new();

    public DeliveryOrchestrator Orchestrator { get; }

    public Task<DeliveryTask> RunAsync(TaskId id, string description = "A customer cancels an order that has not shipped.") =>
        Orchestrator.RunAsync(
            id,
            new FeatureRequest("Cancel an order", description),
            Budget,
            CancellationToken.None);
}
