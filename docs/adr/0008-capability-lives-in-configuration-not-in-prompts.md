# ADR-0008: Capability lives in configuration, not in prompts

**Status:** Accepted · **Date:** 2026-09-19

## Context

"You are the security reviewer and only you may block delivery" is a sentence in
a prompt. A model can ignore it, and a model reading an injected request can be
talked out of it. If authority is expressed only in text, authority is advisory.

## Decision

Every agent carries an `AgentPolicy` — a configuration object, not a prompt:

```csharp
AgentPolicy(AllowedTools, MaxTokensPerStep, CanVeto, RequiresHumanApproval)
```

Three rules are then enforced in code, not asked for in text:

1. **Only an agent with `CanVeto` may raise a blocking finding.**
   `OutputPolicyMiddleware` downgrades `Critical` to `High` for anyone else. A
   backend agent that dislikes a review cannot halt the delivery by saying
   "Critical".
2. **Only an agent with `CanVeto` may be appointed as the gate.** The
   orchestrator *proposes* the plan; `PlanReader` rejects a plan that hands the
   gate to a role without veto power, and falls back to the safe default.
3. **A per-step token ceiling comes from the policy**, and exceeding it fails
   the step rather than returning its work.

There is also a rule the aggregate enforces: the gate agent cannot be one of the
workers. A reviewer must review work it did not produce.

## Consequences

- Prompt injection cannot escalate privilege; at worst it wastes a step.
- Capability is greppable. "What can the tester do?" is one record, not five
  paragraphs of prompt.
- The policies and the prompts can disagree. The policy wins, and the
  disagreement shows up as a logged warning rather than as silent behaviour.
