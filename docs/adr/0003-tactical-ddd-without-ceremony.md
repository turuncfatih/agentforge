# ADR-0003: Tactical DDD, without the ceremony

**Status:** Accepted · **Date:** 2026-09-19

## Context

DDD is easy to over-apply. A small codebase wearing a full tactical-pattern
costume reads as "copied from a book", and reviewers notice. The question is
not "should we do DDD" but "does this domain have invariants worth protecting".

It does. A delivery task must not overspend its budget, must not settle a step
twice, must not approve work the gate blocked, and must not loop forever. Those
are business rules, and if they leak out of one place they stop being enforceable.

## Decision

Apply the tactical patterns that carry weight, and skip the rest.

**Used**

| Pattern | Here |
|---|---|
| Aggregate root | `DeliveryTask` — every state change goes through it |
| Entity | `AgentStep`, inside the aggregate |
| Value objects | `Budget`, `Consumption`, `Money`, `TokenUsage`, `Finding`, `TaskId`, `StepId` |
| Domain events | `GateEvaluated`, `ReworkRequested`, `DeliveryEscalated`, … |
| Domain service | `IGatePolicy` — a rule that belongs to no single entity |
| Repository | `IDeliveryTaskRepository`, one per aggregate |

**Strategic split**

| Context | Type | Why |
|---|---|---|
| Delivery | Core | The differentiator; everything else supports it |
| Evaluation | Supporting | Quality measurement, not the product |
| Observability | Generic | Cost and traces; a solved problem elsewhere |

## Consequences

- Invariants have one home and are covered by tests that name them.
- There is more indirection than a script would need. That is the price of the
  rules being enforceable, and it is only worth paying because the rules exist.

## Rejected

- **A repository per entity.** `AgentStep` is reached through the task that owns it.
- **MediatR for everything.** The one pipeline that needed composing is written
  directly (ADR-0007); a mediator on top would add indirection without removing any.
- **An anemic model with a `DeliveryService`.** That is the version where the
  invariants are one forgotten `if` away from being bypassed.
