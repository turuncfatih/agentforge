# Ubiquitous language

The words below mean exactly one thing, in conversation and in code. Where a
term is also an everyday word, the domain meaning is narrower on purpose.

| Term | Meaning here | In code |
|---|---|---|
| **Feature request** | What a human asked for. The only untrusted input in the system. | `FeatureRequest` |
| **Delivery task** | One request being worked to a conclusion. The consistency boundary. | `DeliveryTask` |
| **Agent** | A *role*, not a model. Swapping the model behind an agent changes nothing about its contract. | `IAgent` |
| **Step** | One agent invocation within a task. Settles exactly once. | `AgentStep` |
| **Round** | One pass of the whole plan. Rework increments it. Part of every step id. | `DeliveryTask.Round` |
| **Plan** | Which agents run, in what shape, for this task. Proposed by a model, validated in code. | `DeliveryPlan` |
| **Blackboard** | The shared, append-only context agents read from. Agents never call each other. | `Blackboard` |
| **Artifact** | Work an agent produced. Opaque to the domain. | `Artifact` |
| **Finding** | An objection raised against the work. Must carry evidence. | `Finding` |
| **Blocking finding** | A finding severe enough to stop delivery. Only a veto-holding agent can raise one. | `Finding.IsBlocking` |
| **Gate** | The review that can stop delivery. Runs after the workers, never alongside them. | `IGatePolicy` |
| **Veto** | The capability to block. Granted by configuration, never claimed in a prompt. | `AgentPolicy.CanVeto` |
| **Rework** | Sending a blocked round back to the workers. Bounded. | `BeginRework` |
| **Budget** | The ceiling a task may spend: steps, tokens, money, rework loops. | `Budget` |
| **Consumption** | What has actually been spent. | `Consumption` |
| **Escalated** | A terminal outcome meaning "a human should decide". Not an error. | `DeliveryState.Escalated` |
| **Checkpoint** | Persisting the aggregate after a state change, so a crash resumes. | `IDeliveryTaskRepository.SaveAsync` |

## Words avoided

- **"Prompt"** — never appears in the domain. See [ADR-0004](adr/0004-the-domain-does-not-know-that-language-models-exist.md).
- **"Manager" / "Helper" / "Processor"** — a class that needs one of these has not been named yet.
- **"AI"** as a noun in code. The domain records that work happened, not what produced it.
