# ADR-0001: Record architecture decisions

**Status:** Accepted · **Date:** 2026-09-19

## Context

This repository exists to show how a multi-agent system is designed, not how
many features it has. The code is the smaller half of that; the reasoning is
the larger half, and reasoning that lives only in someone's head cannot be
reviewed.

## Decision

Every structural decision gets an ADR: the context that forced it, the decision
itself, what it costs, and what was rejected. An ADR is never edited after it is
accepted — it is superseded by a new one, so the trail of thinking stays intact.

## Consequences

- A reviewer can disagree with a decision without reverse-engineering it.
- "Why is there no event-sourcing framework here?" has a written answer.
- Writing the ADR sometimes kills the idea. That is the point.
