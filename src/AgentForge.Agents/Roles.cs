using AgentForge.Application.Agents;
using AgentForge.Domain.Delivery;
using AgentForge.Llm;

namespace AgentForge.Agents;

/// <summary>
/// Designs the change. Produces artifacts, never findings: an author who can
/// also raise objections about their own work is not a review.
/// </summary>
public sealed class BackendAgent(IStructuredModel model) : LlmAgent(model)
{
    public override AgentId Id => AgentId.Backend;

    public override AgentPolicy Policy => AgentPolicy.ReadOnly(maxTokens: 20_000, "repo.read", "schema.validate");

    protected override string SystemPrompt =>
        """
        You are the backend engineer on a delivery team.
        Produce a concrete technical design: data model changes, endpoints, state
        transitions, failure modes and idempotency concerns.
        Be specific enough that another engineer could implement it without asking
        a follow-up question. Do not raise findings; that is the reviewer's job.
        """;
}

/// <summary>
/// Turns the design into checks. Also produces no findings: a failing test is
/// evidence for the gate, not a veto of its own.
/// </summary>
public sealed class TesterAgent(IStructuredModel model) : LlmAgent(model)
{
    public override AgentId Id => AgentId.Tester;

    public override AgentPolicy Policy => AgentPolicy.ReadOnly(maxTokens: 15_000, "repo.read", "test.plan");

    protected override string SystemPrompt =>
        """
        You are the test engineer on a delivery team.
        Derive executable acceptance criteria from the request and the proposed
        design: happy path, boundary conditions, concurrency and failure cases.
        State the expected observable outcome for each case.
        """;
}

/// <summary>
/// The only agent with veto power. Everything it blocks on must carry evidence,
/// because the gate is allowed to stop delivery and an opinion is not enough.
/// </summary>
public sealed class SecurityAgent(IStructuredModel model) : LlmAgent(model)
{
    public override AgentId Id => AgentId.Security;

    public override AgentPolicy Policy => AgentPolicy.Gatekeeper(maxTokens: 20_000, "repo.read", "cve.lookup");

    protected override string SystemPrompt =>
        """
        You are the security reviewer, and the only role that can block delivery.
        Review the proposed design for authorization gaps, injection, data exposure,
        unsafe defaults and missing audit trails.
        Reserve Critical for issues that would be unsafe to ship; use High for real
        problems that can follow later. Every finding must cite the specific part of
        the design it refers to. Produce findings, not designs.
        """;
}

/// <summary>Writes the closing report once the gate has passed. No veto, no design.</summary>
public sealed class AnalystAgent(IStructuredModel model) : LlmAgent(model)
{
    public override AgentId Id => AgentId.Analyst;

    public override AgentPolicy Policy => AgentPolicy.ReadOnly(maxTokens: 12_000, "metrics.read");

    protected override string SystemPrompt =>
        """
        You are the delivery analyst.
        Summarise what was decided, what was objected to and how it was resolved,
        and list the residual risks that were knowingly accepted.
        Write for someone who will read this months later without context.
        """;
}
