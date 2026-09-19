using AgentForge.Agents;
using AgentForge.Agents.Demo;
using AgentForge.Api;
using AgentForge.Application.Agents;
using AgentForge.Application.Agents.Middleware;
using AgentForge.Application.Orchestration;
using AgentForge.Application.Ports;
using AgentForge.Domain.Delivery;
using AgentForge.Infrastructure.Observability;
using AgentForge.Infrastructure.Persistence;
using AgentForge.Llm;
using Microsoft.Extensions.AI;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- composition root
// Every dependency direction in this file points inwards: the API knows the
// adapters, the adapters know the ports, and the domain knows none of them.

builder.Services.AddSingleton(TimeProvider.System);

// The provider boundary. Swapping this single line for a real IChatClient is
// the only change needed to run the same pipeline against a live model.
builder.Services.AddSingleton<IChatClient>(_ => DemoScript.OrderCancellation());
builder.Services.AddSingleton(new ModelPricing("scripted-demo", InputPerMillion: 3.00m, OutputPerMillion: 15.00m));
builder.Services.AddSingleton<IStructuredModel>(sp => new StructuredModel(
    sp.GetRequiredService<IChatClient>(),
    sp.GetRequiredService<ModelPricing>()));

// The five roles.
builder.Services.AddSingleton<IAgent, OrchestratorAgent>();
builder.Services.AddSingleton<IAgent, BackendAgent>();
builder.Services.AddSingleton<IAgent, TesterAgent>();
builder.Services.AddSingleton<IAgent, SecurityAgent>();
builder.Services.AddSingleton<IAgent, AnalystAgent>();
builder.Services.AddSingleton<AgentRegistry>();
builder.Services.AddSingleton<IAgentRegistry>(sp => sp.GetRequiredService<AgentRegistry>());
builder.Services.AddSingleton<IPlanReader, PlanReader>();

// Cross-cutting concerns, in the order documented in docs/adr/0007.
builder.Services.AddSingleton(sp => new AgentPipeline(
[
    ActivatorUtilities.CreateInstance<TelemetryMiddleware>(sp),
    ActivatorUtilities.CreateInstance<PromptInjectionShield>(sp),
    ActivatorUtilities.CreateInstance<BudgetGuardMiddleware>(sp),
    ActivatorUtilities.CreateInstance<RetryMiddleware>(sp),
    ActivatorUtilities.CreateInstance<OutputPolicyMiddleware>(sp),
]));

builder.Services.AddSingleton<IGatePolicy>(_ => SeverityThresholdGate.BlockOnCritical);
builder.Services.AddSingleton<IDeliveryTaskRepository, InMemoryDeliveryTaskRepository>();
builder.Services.AddSingleton<DeliveryEventLog>();
builder.Services.AddSingleton<IDeliveryEventSink>(sp => sp.GetRequiredService<DeliveryEventLog>());
builder.Services.AddScoped<DeliveryOrchestrator>();

var app = builder.Build();

// ---------------------------------------------------------------- endpoints

app.MapGet("/", () => Results.Ok(new
{
    service = "AgentForge",
    what = "A multi-agent delivery pipeline: orchestrator plans, specialists work in parallel, a security gate can veto, and an analyst reports.",
    endpoints = new[]
    {
        "POST /deliveries            - run a delivery to completion",
        "GET  /deliveries/{id}       - the current state of a delivery",
        "GET  /deliveries/{id}/events - the append-only audit trail, streamed",
        "GET  /pipeline              - the middleware order every agent call passes through",
    },
}));

app.MapGet("/pipeline", (AgentPipeline pipeline) => Results.Ok(new { stages = pipeline.Stages }));

app.MapPost("/deliveries", async (
    StartDelivery body,
    DeliveryOrchestrator orchestrator,
    CancellationToken cancellationToken) =>
{
    var id = TaskId.New();
    var request = new FeatureRequest(body.Title, body.Description, body.AcceptanceCriteria ?? []);
    var task = await orchestrator.RunAsync(id, request, Budget.Default, cancellationToken);
    return Results.Ok(DeliveryView.From(task));
});

app.MapGet("/deliveries/{id}", async (
    string id,
    IDeliveryTaskRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!Guid.TryParse(id, out var guid))
    {
        return Results.BadRequest(new { error = "id must be a GUID" });
    }

    var task = await repository.FindAsync(new TaskId(guid), cancellationToken);
    return task is null ? Results.NotFound() : Results.Ok(DeliveryView.From(task));
});

app.MapGet("/deliveries/{id}/events", (
    string id,
    DeliveryEventLog log,
    CancellationToken cancellationToken) =>
{
    if (!Guid.TryParse(id, out var guid))
    {
        return Results.BadRequest(new { error = "id must be a GUID" });
    }

    var stream = log
        .SubscribeAsync(new TaskId(guid), cancellationToken)
        .Select(e => new { seq = e.Sequence, type = e.Type });

    return TypedResults.ServerSentEvents(stream, eventType: "delivery");
});

app.Run();

/// <summary>Exposed so the integration tests can spin the real host up.</summary>
public partial class Program;
