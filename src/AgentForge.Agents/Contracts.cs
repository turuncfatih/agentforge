namespace AgentForge.Agents;

/// <summary>
/// The wire shape agents ask the model for. Kept separate from the domain
/// types on purpose: an LLM cannot be trusted to satisfy a domain invariant,
/// so its output is parsed into a DTO first and only then validated into
/// domain objects that refuse to be malformed.
/// </summary>
public sealed record AgentOutputDto
{
    public string Summary { get; init; } = string.Empty;

    public List<ArtifactDto> Artifacts { get; init; } = [];

    public List<FindingDto> Findings { get; init; } = [];
}

public sealed record ArtifactDto
{
    public string Kind { get; init; } = "note";

    public string Title { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;
}

public sealed record FindingDto
{
    /// <summary>Info | Low | Medium | High | Critical. Anything else is treated as Info.</summary>
    public string Severity { get; init; } = "Info";

    public string Category { get; init; } = "general";

    public string Title { get; init; } = string.Empty;

    public string Evidence { get; init; } = string.Empty;

    public string? SuggestedFix { get; init; }
}

public sealed record PlanDto
{
    public List<string> Workers { get; init; } = [];

    public string Gate { get; init; } = "security";

    public string Reporter { get; init; } = "analyst";
}
