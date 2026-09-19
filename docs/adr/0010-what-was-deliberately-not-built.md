# ADR-0010: What was deliberately not built

**Status:** Accepted · **Date:** 2026-09-19

## Context

A portfolio repository is judged as much by its restraint as by its output.
Everything below was considered and left out on purpose. Leaving them
undocumented would look like an oversight, so this ADR records the reasoning.

## Not built, and why

| Left out | Reasoning |
|---|---|
| **A real database** | The port is what the application depends on. `InMemoryDeliveryTaskRepository` implements it, including the optimistic-concurrency check, so swapping in EF Core changes one registration and no application code. Persisting a schema would add volume, not insight. |
| **Tool execution** | Tools are modelled (`AllowedTools` on the policy) but not executed. A sandbox worth shipping is its own project; a fake one would be theatre. |
| **A workflow engine** | Deterministic step ids plus checkpoints give resume and idempotency without the dependency. See ADR-0006. |
| **Full event sourcing** | The step log is already append-only and replayable — event sourcing's useful half. A framework, projections and a versioning story would be the expensive half. |
| **A UI** | The audit trail streams over SSE. A dashboard would demonstrate frontend work, which is not what this repository claims. |
| **Live provider credentials** | Every test runs against `ScriptedChatClient`. A suite that needs an API key is a suite that stops being run. |
| **Vector store / RAG** | Nothing here needs retrieval. Adding it to look current would be the same mistake as over-applying DDD. |
| **More than five agents** | The interesting properties are parallel fan-out, a veto and a bounded loop. A sixth agent demonstrates none of them. |

## The honest limitations

- `InMemoryDeliveryTaskRepository` loses everything on restart. The resume path
  is real and tested; the storage behind it is not durable yet.
- The injection shield is pattern-based. It lowers the blast radius of a hostile
  request; it does not make injection impossible, and it is documented as
  defence in depth rather than a solution.
- The evaluation bounded context is named in ADR-0003 but not implemented. It is
  a boundary that has been reserved, not a feature that has been shipped.
