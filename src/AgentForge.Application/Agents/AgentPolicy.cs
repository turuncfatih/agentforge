namespace AgentForge.Application.Agents;

/// <summary>
/// What an agent is allowed to do. Capability is configuration, not a prompt
/// instruction: an agent cannot talk its way into a tool it was not granted.
/// </summary>
public sealed record AgentPolicy(
    IReadOnlySet<string> AllowedTools,
    int MaxTokensPerStep,
    bool CanVeto,
    bool RequiresHumanApproval)
{
    public static AgentPolicy ReadOnly(int maxTokens, params string[] tools) =>
        new(new HashSet<string>(tools, StringComparer.Ordinal), maxTokens, CanVeto: false, RequiresHumanApproval: false);

    public static AgentPolicy Gatekeeper(int maxTokens, params string[] tools) =>
        new(new HashSet<string>(tools, StringComparer.Ordinal), maxTokens, CanVeto: true, RequiresHumanApproval: false);

    public bool Allows(string tool) => AllowedTools.Contains(tool);
}
