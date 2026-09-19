using AgentForge.Llm.Fake;

namespace AgentForge.Agents.Demo;

/// <summary>
/// The canned run used by the API demo and by the orchestration tests.
///
/// It is written to exercise the interesting path rather than the happy one:
/// the security agent blocks the first round on a missing authorization check,
/// the backend agent revises the design, and the second review passes. That is
/// the rework loop, the gate and the budget all firing in one request.
/// </summary>
public static class DemoScript
{
    public static ScriptedChatClient OrderCancellation() => ScriptedChatClient.Create()
        .WhenPromptContains(
            "You are the delivery orchestrator",
            """
            {"summary":"Design and tests in parallel, security gate, analyst report.",
             "artifacts":[{"kind":"plan","title":"Delivery plan",
                "body":"{\"workers\":[\"backend\",\"tester\"],\"gate\":\"security\",\"reporter\":\"analyst\"}"}],
             "findings":[]}
            """)
        .WhenPromptContains(
            "You are the backend engineer",
            """
            {"summary":"Cancellation endpoint and state transition.",
             "artifacts":[{"kind":"design","title":"Order cancellation design",
                "body":"POST /orders/{id}/cancel moves an Order from Placed to Cancelled. Adds CancelledAt and CancellationReason. Idempotent on a client-supplied request id."}],
             "findings":[]}
            """,
            """
            {"summary":"Revised: ownership check and audit trail added.",
             "artifacts":[{"kind":"design","title":"Order cancellation design (revised)",
                "body":"POST /orders/{id}/cancel authorises the caller against Order.CustomerId before the transition, and records an OrderCancelled audit event carrying actor, reason and request id. Refund is initiated through the existing outbox."}],
             "findings":[]}
            """)
        .WhenPromptContains(
            "You are the test engineer",
            """
            {"summary":"Acceptance criteria for cancellation.",
             "artifacts":[{"kind":"test-plan","title":"Cancellation acceptance criteria",
                "body":"1) Owner cancels a Placed order -> 200, state Cancelled. 2) Non-owner -> 403, state unchanged. 3) Order already Shipped -> 409. 4) Duplicate request id -> exactly one cancellation, 200 both times. 5) Concurrent cancel and ship -> one wins, no partial state."}],
             "findings":[]}
            """)
        .WhenPromptContains(
            "You are the security reviewer",
            """
            {"summary":"One blocking issue.",
             "artifacts":[],
             "findings":[{"severity":"Critical","category":"authorization",
                "title":"Cancellation endpoint does not verify order ownership",
                "evidence":"The design for POST /orders/{id}/cancel describes the state transition but never checks the caller against Order.CustomerId, so any authenticated user could cancel any order.",
                "suggestedFix":"Authorise the caller against Order.CustomerId before the transition, and record an audit event."}]}
            """,
            """
            {"summary":"Blocking issue resolved; one minor note.",
             "artifacts":[],
             "findings":[{"severity":"Low","category":"observability",
                "title":"Cancellation reason is unconstrained free text",
                "evidence":"The revised design stores CancellationReason as free text, which will make cancellation reporting unreliable once volume grows.",
                "suggestedFix":"Use a controlled vocabulary with an 'Other' escape hatch."}]}
            """)
        .WhenPromptContains(
            "You are the delivery analyst",
            """
            {"summary":"Delivered after one rework round.",
             "artifacts":[{"kind":"report","title":"Delivery report",
                "body":"Scope: customer-initiated order cancellation. The first design omitted an ownership check and was blocked by the security gate. The revised design authorises against Order.CustomerId and emits an audit event; the second review passed. Accepted residual risk: cancellation reason remains free text, tracked as a low-severity follow-up."}],
             "findings":[]}
            """)
        .Otherwise("""{"summary":"no scripted reply","artifacts":[],"findings":[]}""");
}
