# ADR-0004: The domain does not know that language models exist

**Status:** Accepted · **Date:** 2026-09-19

## Context

This is the decision the rest of the repository is built around.

The usual failure in an LLM codebase is gradual: a prompt string appears in a
service, then a `ChatMessage` in a method signature, then a vendor SDK type in
the business rules. Nothing breaks on the day it happens. Six months later the
rules cannot be tested without a network call, cannot be reasoned about without
reading a prompt, and cannot survive a change of provider.

## Decision

`AgentForge.Domain` references **nothing but the base class library** — no SDK,
no logging, no DI, no serializer.

The domain records **that work happened and what it cost**:

> "Step `security:r0` settled with outcome Completed, one Critical finding,
> 565 tokens, $0.0032."

It does not know whether that came from a model, a rules engine, or a person.
`AgentStep` has no prompt, no model id and no vendor. `TokenUsage` and `Money`
are domain concepts because *budget* is a business rule (ADR-0009), but they are
plain numbers with no idea where they came from.

The dependency graph enforces the direction:

```
Domain   ← Application ← Agents → Llm
                ↑                  ↑
          Infrastructure           └── Microsoft.Extensions.AI
```

`Agents` is the only assembly that references both `Domain` and `Llm`. That is
the one place an `LlmUsage` becomes a `TokenUsage`.

## Consequences

- Every delivery rule is testable offline, in milliseconds, for free.
- Changing the provider touches one registration.
- Two extra usage types exist (`LlmUsage` and `TokenUsage`) that look almost
  identical. Deliberate: the moment they are merged, the boundary is gone.

## Enforcement

`ArchitectureTests` fails the build if the domain ever references a non-BCL
assembly, or if any public domain member is named after a prompt, a model or a
vendor. A comment is a wish; a failing test is a boundary.
