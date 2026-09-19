# ADR-0009: Budget is a domain concept, not an infrastructure detail

**Status:** Accepted · **Date:** 2026-09-19

## Context

Cost control in an LLM system is usually bolted on: a counter in a service, an
alert on a dashboard, a rate limit at the gateway. All three share a flaw — they
treat running out of money as an operational error.

It is not. "This request has consumed what it was worth; a human should decide
whether to continue" is a **business rule**, and it deserves the same treatment
as any other business rule.

## Decision

`Budget` is a value object on the aggregate, with four ceilings:

```csharp
Budget(MaxSteps, MaxTokens, MaxCost, MaxReworkLoops)
```

`Consumption` accumulates against it and reports *which* ceiling was hit.
`DeliveryTask.OpenStep` refuses to open a step once any ceiling is reached, and
the terminal state is `Escalated` — a legitimate outcome with a reason attached,
not an exception.

Two details that turned out to matter, both caught by tests:

- **The check is per step, not per round.** A round opens several steps, so
  checking once at the top of the loop overspends by a whole round.
- **Validation lives in the `init` accessors**, so `budget with { MaxSteps = 0 }`
  is rejected too. A guard that only runs in the constructor has a hole in it.

Planning is charged like any other step. A planner that quietly burns tokens
outside the budget is how "why was the bill so high?" begins.

## Consequences

- The most-asked production question — "what stops this costing a fortune?" —
  has a one-line answer backed by a named test.
- Every ceiling is visible in the API response, per step and in total.
- Cost estimation is only as good as the configured `ModelPricing`. It is
  computed where the tokens are counted, which is the only place that can.
