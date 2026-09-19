# ADR-0006: Deterministic step ids instead of a workflow engine

**Status:** Accepted · **Date:** 2026-09-19

## Context

An agent run is long, expensive and interruptible. A process that dies halfway
must not restart from zero — at LLM prices, restarting a run is paying twice for
the same answer.

The standard answer is a durable execution engine (Temporal, Durable Functions,
Elsa). They are good, and they are a large dependency plus an operational
commitment.

## Decision

Get most of the benefit from one property: **step ids are derived, not generated.**

```csharp
StepId.For(task, agent, round)   // "01a0b9b3:backend:r1"
```

The same task, the same agent and the same round always produce the same id.
From that, three things follow:

- **Resume.** Before running an agent, the orchestrator asks the aggregate
  `HasSettledStep(agent)`. Work that already settled is skipped, not repeated.
- **Idempotency.** `AgentStep.Settle` throws if the step already settled, so a
  duplicate delivery of the same result cannot be charged twice.
- **Replay.** The domain event log is append-only, and replaying it reconstructs
  the same state — event sourcing's useful half, without the framework.

The repository is checkpointed after every state change, and events are
published only after the save, so a crash between the two replays an event
rather than losing state.

## Consequences

- `RunAsync` is safe to call twice with the same id. A test asserts that the
  second call makes zero model calls.
- No distributed timers, no long-running workflow versioning story. If those are
  needed later, the ports are already in place.
- Optimistic concurrency is checked in the repository, because losing a
  concurrent write is a bug that only shows up once there is a real database.
