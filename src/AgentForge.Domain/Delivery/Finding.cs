using AgentForge.Domain.Abstractions;

namespace AgentForge.Domain.Delivery;

public enum Severity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4,
}

/// <summary>
/// An objection raised against the work produced so far.
/// Evidence is mandatory: a finding without evidence is an opinion, and the
/// gate must not be allowed to block delivery on an opinion.
/// </summary>
public sealed record Finding
{
    public Finding(AgentId raisedBy, Severity severity, string category, string title, string evidence, string? suggestedFix = null)
    {
        RaisedBy = raisedBy;
        Severity = severity;
        Category = Ensure.NotBlank(category, nameof(category));
        Title = Ensure.NotBlank(title, nameof(title));
        Evidence = Ensure.NotBlank(evidence, nameof(evidence));
        SuggestedFix = suggestedFix;
    }

    public AgentId RaisedBy { get; }
    public Severity Severity { get; }
    public string Category { get; }
    public string Title { get; }
    public string Evidence { get; }
    public string? SuggestedFix { get; }

    public bool IsBlocking => Severity >= Severity.Critical;
}
