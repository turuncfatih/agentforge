# ADR-0002: Supervisor orchestration with a blocking gate

**Status:** Accepted · **Date:** 2026-09-19

## Context

Five agents need to collaborate on one delivery. The common shapes are:

1. **Chat room** — every agent sees every message and speaks when it wants.
2. **Chain** — a fixed hand-off from agent to agent.
3. **Supervisor** — one orchestrator plans, dispatches, and decides what happens next.

Shape 1 has no stopping condition and no accountability: when the output is
wrong, nobody owns it. Shape 2 cannot express "these two can run at the same
time", and a failure in the middle strands the work.

## Decision

A **supervisor** sequences the work, with three additions:

- **Parallel fan-out** for agents that do not depend on each other (backend and
  tester both read the request; neither reads the other).
- **A blocking gate.** One agent — security — reviews work it did not produce
  and can stop delivery. The gate runs after the workers, never alongside them.
- **A bounded rework loop.** A blocked gate sends the round back at most
  `MaxReworkLoops` times, then the task goes to a human.

Agents never call each other. They publish to a shared blackboard, and the
orchestrator decides who reads what.

## Consequences

- The graph is a state machine, so "where is this delivery?" is answerable.
- Adding a sixth agent is a plan change, not a rewrite.
- The orchestrator is a bottleneck by design. For this workload that is the
  right trade: it is the thing that makes the run explainable.

## Rejected

- **Autonomous agent negotiation.** No stopping condition, no cost ceiling, and
  no way to test. It is the demo that never reaches production.
- **A workflow engine** (Temporal, Elsa, Durable Functions). See ADR-0006.
