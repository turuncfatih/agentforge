using AgentForge.Application.Agents;
using AgentForge.Application.Ports;
using AgentForge.Domain.Delivery;

namespace AgentForge.Agents;

public sealed class AgentRegistry : IAgentRegistry
{
    private readonly Dictionary<AgentId, IAgent> _agents;

    public AgentRegistry(IEnumerable<IAgent> agents) =>
        _agents = agents.ToDictionary(a => a.Id);

    public IReadOnlyCollection<AgentId> Known => _agents.Keys;

    public IAgent Resolve(AgentId id) =>
        _agents.TryGetValue(id, out var agent)
            ? agent
            : throw new InvalidOperationException(
                $"No agent registered for role '{id}'. Registered: {string.Join(", ", _agents.Keys)}.");

    public bool Knows(AgentId id) => _agents.ContainsKey(id);
}
