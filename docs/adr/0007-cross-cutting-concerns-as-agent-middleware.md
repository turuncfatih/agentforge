# ADR-0007: Cross-cutting concerns as agent middleware

**Status:** Accepted · **Date:** 2026-09-19

## Context

Telemetry, retries, spend ceilings, injection defence and output validation
apply to every agent. Two wrong homes for them: inside each agent (duplicated
five times, drifting), or inside the prompt ("please do not exceed your budget",
which is a request, not a control).

## Decision

Wrap every agent call in a middleware chain, shaped like ASP.NET's:

```
telemetry → injection-shield → budget-guard → retry → output-policy → agent
```

| Stage | Responsibility |
|---|---|
| `telemetry` | One span per step, tagged with tokens and cost |
| `injection-shield` | Neutralises instruction-like text in the untrusted request |
| `budget-guard` | Enforces the per-step token ceiling from the agent's policy |
| `retry` | Exponential backoff, **accumulating the spend of failed attempts** |
| `output-policy` | Validates the response against the agent's own capabilities |

The order is load-bearing, so a test asserts it.

Two details that are easy to get wrong:

- **Retry sits inside the budget guard.** Tokens burned by failed attempts are
  carried into the returned response, so a retry storm is visible and capped.
  Dropping them would make the most expensive run look like the cheapest one.
- **`output-policy` sits closest to the agent**, because it judges raw output
  before any other stage has reshaped it.

## Consequences

- A new concern is a new class and one line of registration.
- `GET /pipeline` returns the live chain, so the deployed order is inspectable.
- Middleware runs per call, so a stage that blocks is a stage that costs. They
  are kept cheap and synchronous where possible.
