using AgentForge.Domain.Abstractions;

namespace AgentForge.Domain.Delivery;

/// <summary>
/// A piece of work an agent produced. The domain treats the body as opaque
/// text; it never parses it and never knows how it was generated.
/// </summary>
public sealed record Artifact
{
    public Artifact(AgentId producedBy, string kind, string title, string body)
    {
        ProducedBy = producedBy;
        Kind = Ensure.NotBlank(kind, nameof(kind));
        Title = Ensure.NotBlank(title, nameof(title));
        Body = Ensure.NotBlank(body, nameof(body));
    }

    public AgentId ProducedBy { get; }
    public string Kind { get; }
    public string Title { get; }
    public string Body { get; }
}
