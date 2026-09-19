using System.Text.Json;
using AgentForge.Application.Ports;
using AgentForge.Domain.Abstractions;
using AgentForge.Domain.Delivery;
using Microsoft.Extensions.Logging;

namespace AgentForge.Agents;

/// <summary>
/// Validates the orchestrator's proposed plan before the aggregate ever sees it.
///
/// This is the seam where a language model's suggestion becomes a system
/// decision. Three things are checked in code, not asked for in a prompt:
/// the roles exist, the gate actually holds veto power, and the plan satisfies
/// the aggregate's own rules. Anything else falls back to the default plan.
/// </summary>
public sealed class PlanReader(AgentRegistry registry, ILogger<PlanReader> logger) : IPlanReader
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static DeliveryPlan Default { get; } = new(
        parallelWorkers: [AgentId.Backend, AgentId.Tester],
        gate: AgentId.Security,
        reporter: AgentId.Analyst);

    public DeliveryPlan Read(IReadOnlyList<Artifact> artifacts)
    {
        var proposal = artifacts.LastOrDefault(a =>
            a.ProducedBy == AgentId.Orchestrator &&
            a.Kind.Equals(OrchestratorAgent.PlanArtifactKind, StringComparison.OrdinalIgnoreCase));

        if (proposal is null)
        {
            logger.LogWarning("orchestrator produced no plan artifact; using the default plan");
            return Default;
        }

        try
        {
            var dto = JsonSerializer.Deserialize<PlanDto>(proposal.Body, Json)
                      ?? throw new JsonException("plan body deserialised to null");

            return Validate(dto);
        }
        catch (Exception ex) when (ex is JsonException or DomainException or InvalidOperationException)
        {
            logger.LogWarning(ex, "rejected the proposed plan; falling back to the default plan");
            return Default;
        }
    }

    private DeliveryPlan Validate(PlanDto dto)
    {
        var workers = dto.Workers.Select(w => new AgentId(w.Trim().ToLowerInvariant())).ToArray();
        var gate = new AgentId(dto.Gate.Trim().ToLowerInvariant());
        var reporter = new AgentId(dto.Reporter.Trim().ToLowerInvariant());

        foreach (var role in workers.Append(gate).Append(reporter))
        {
            if (!registry.Knows(role))
            {
                throw new InvalidOperationException($"plan names unknown role '{role}'");
            }
        }

        if (!registry.Resolve(gate).Policy.CanVeto)
        {
            throw new InvalidOperationException(
                $"plan appoints '{gate}' as the gate, but that role has no veto power. "
                + "Capability comes from configuration, not from the plan.");
        }

        // The aggregate has the final word: its constructor enforces the rest.
        return new DeliveryPlan(workers, gate, reporter);
    }
}
