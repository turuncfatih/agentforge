using AgentForge.Domain.Delivery;

namespace AgentForge.Api;

public sealed record StartDelivery(string Title, string Description, IReadOnlyList<string>? AcceptanceCriteria);

/// <summary>
/// A flat read model for the wire. Domain types are deliberately not serialised
/// directly: the HTTP contract should be free to change without dragging the
/// aggregate's shape along with it.
/// </summary>
public sealed record DeliveryView(
    string Id,
    string Title,
    string State,
    int Round,
    string? TerminalReason,
    int Steps,
    long Tokens,
    string Cost,
    IReadOnlyList<StepView> Timeline,
    IReadOnlyList<FindingView> Findings,
    IReadOnlyList<ArtifactView> Artifacts)
{
    public static DeliveryView From(DeliveryTask task) => new(
        task.Id.Value.ToString(),
        task.Request.Title,
        task.State.ToString(),
        task.Round,
        task.TerminalReason,
        task.Consumption.Steps,
        task.Consumption.Tokens.Total,
        task.Consumption.Cost.ToString(),
        [.. task.Steps.Select(s => new StepView(s.Id.ToString(), s.Agent.Value, s.Round, s.Outcome?.ToString() ?? "Open", s.Tokens.Total, s.Cost.ToString()))],
        [.. task.AllFindings.Select(f => new FindingView(f.Severity.ToString(), f.RaisedBy.Value, f.Category, f.Title, f.Evidence, f.SuggestedFix))],
        [.. task.AllArtifacts.Select(a => new ArtifactView(a.ProducedBy.Value, a.Kind, a.Title, a.Body))]);
}

public sealed record StepView(string Id, string Agent, int Round, string Outcome, long Tokens, string Cost);

public sealed record FindingView(string Severity, string RaisedBy, string Category, string Title, string Evidence, string? SuggestedFix);

public sealed record ArtifactView(string ProducedBy, string Kind, string Title, string Body);
