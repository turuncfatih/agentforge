# ADR-0005: Microsoft.Extensions.AI as the provider boundary

**Status:** Accepted · **Date:** 2026-09-19

## Context

Three options for talking to a model from .NET:

1. A hand-rolled `ILlmProvider` interface.
2. A full agent framework (Semantic Kernel, and friends).
3. `Microsoft.Extensions.AI`'s `IChatClient`.

## Decision

Use **`IChatClient` as the boundary, and write the orchestration on top of it.**

`IChatClient` is the ecosystem's standard abstraction — the same shape every
provider ships an adapter for, and the shape the higher-level frameworks
themselves build on. Taking it means a live provider is a one-line swap.

Not taking a full framework means the parts this repository is actually about —
the gate, the budget, the rework loop, the middleware — stay ours, visible and
testable, instead of being configuration inside somebody else's engine.

On top of `IChatClient` sits one thin layer, `StructuredModel`, which does two
things worth owning:

- **Typed results only.** Agents ask for a shape; free-form text never leaves
  the LLM layer.
- **One repair round-trip.** A malformed first answer is re-asked with the
  parser's own error as feedback, rather than failing a whole step over a
  missing brace.

## Consequences

- Current on the ecosystem, without outsourcing the interesting decisions.
- The repair loop costs a second call in the bad case, and it is metered.

## Rejected

- **A bespoke `ILlmProvider`.** Reinvents the standard and reads as not having
  looked at what .NET already offers.
- **Semantic Kernel as the orchestrator.** It would make most of the decisions
  this repository exists to demonstrate.
