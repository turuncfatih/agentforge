using AgentForge.Domain.Abstractions;

namespace AgentForge.Domain.Delivery;

/// <summary>The thing a human asked for. The only untrusted input in the domain.</summary>
public sealed record FeatureRequest
{
    public FeatureRequest(string title, string description, IReadOnlyList<string>? acceptanceCriteria = null)
    {
        Title = Ensure.NotBlank(title, nameof(title));
        Description = Ensure.NotBlank(description, nameof(description));
        AcceptanceCriteria = acceptanceCriteria ?? [];
    }

    public string Title { get; }
    public string Description { get; }
    public IReadOnlyList<string> AcceptanceCriteria { get; }
}
