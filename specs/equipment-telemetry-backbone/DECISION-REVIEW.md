# Decision review brief — Spec: Equipment telemetry backbone

- Source: `specs/equipment-telemetry-backbone/SPEC.md (## Decisions)`
- Generated: 2026-11-18T00:18:34Z
- Detected type: integration, architecture
- Readiness: **CHALLENGE** (6/6 record elements)
- This brief is input to judgment, not a verdict. The agent fills the scorecard after reading the code.

**Reversibility:** stated

**Alternatives on record (verify they are credible, not strawmen):**
- Throughput and latency matter, but correctness and operator visibility matter more for the first release. - Options considered: a single ASP.NET Core modular mo
- separate API and worker deployables
- The simplest reversible alternative is a modular monolith, but separate deployables better match the event-driven target architecture without adding framework c
- The contract must be language-neutral enough for future services while remaining easy to validate. - Options considered: custom JSON envelope vs.

## Evidence signals (heuristic — verify before acting)

- no code paths referenced — the review is document-only

## Design moves to test

- Is an anti-corruption layer needed, or will the external model leak inward?
- What happens on timeout, duplicate delivery, partial failure, and replay?
- Can this be async where the business allows it, removing temporal coupling?
- Can this be a module inside the current deployable before it becomes a service?
- Which dependency can be inverted so the core owns the interface?
- What is the smallest reversible step that tests the boundary?
- Invert a dependency so the unstable thing depends on the stable thing.
- Make an implicit boundary or invariant explicit before adding mechanism.
- Shrink the decision: can half of it ship independently and reversibly?

## Challenge questions

### 1. Problem & forces
- What user/business outcome does this serve, and what breaks if we do nothing?
- Which quality attributes actually drive it (correctness, latency, cost, operability, compliance, time-to-market)? Rank them.
- Which constraints are fixed (budget, deadline, team, contracts) and which are assumed?
- Who owns this decision, and who must live with the consequences?
**Good looks like:** The dominant force is named, and "do nothing" is treated as a real option.

### 2. Alternatives & tradeoffs
- What are at least two credible alternatives, including the simplest thing that could work?
- For each: what does it make easy, what does it make hard, and what does it cost to reverse?
- Is this a one-way or two-way door? If one-way, what evidence justifies it?
- What would have to be true for the chosen option to be wrong?
**Good looks like:** A tradeoff matrix names winners and losers; reversibility is explicit; no strawman options.

### 3. Domain fit (DDD)
- Which bounded context owns this concept, and is the boundary drawn around language or deployment?
- Which terms are overloaded across contexts, and where is the translation (anti-corruption layer)?
- What are the aggregates, their invariants, and the transaction boundary? Does one transaction span aggregates?
- Are domain events the right integration mechanism, or is this a synchronous dependency?
- Does the model carry behavior, or is it an anemic data bag?
**Good looks like:** Ubiquitous language is explicit, invariants are protected at the boundary, external models are translated.

### 4. Architecture & boundaries
- Which direction do dependencies point? Can the core be tested without the framework, DB, or network?
- What is the smallest boundary that isolates this change (module, port, package)?
- What happens when each external dependency is slow, down, or returns garbage?
- Where does data ownership live, and is there a second writer?
- What is the operational story: config, migrations, rollout, rollback, observability?
- What is the failure isolation boundary and idempotency key?
**Good looks like:** Policy depends on nothing; mechanisms are adapters; failure and migration paths are named.

### 5. Testable design
- Where are the seams? Can time, randomness, IO, and external calls be injected?
- Which behaviors get fast unit tests, which need integration, which need contract tests?
- Does each acceptance criterion map to an observable check, or only to implementation details?
- Are the tests deterministic and independent (no shared state, no sleeps)?
- What is the failure signal when this decision turns out wrong in production?
**Good looks like:** Behavior-first tests, deterministic seams, scenario-to-test traceability, a named failure signal.

### 6. Craftsmanship
- Is this the smallest reversible step, or a big-bang change?
- Are names from the domain, or from the framework/table?
- Is the error model explicit (typed failures vs exceptions vs silent nulls)?
- Is there speculative abstraction (interfaces with one implementation, config nobody sets)?
- Is the migration path documented and rollback-able?
**Good looks like:** Small steps, intention-revealing names, explicit errors, no speculative generality, documented migration.

### 7. Decision record
- Are status, owner, and date recorded?
- Are consequences (positive and negative) written down?
- Is there a revisit trigger (metric, date, event)?
- Are links to evidence, code, and related ADRs present?
**Good looks like:** A future reader can tell why, what was traded, and when to revisit.

## Scorecard (agent fills; 1 = poor, 5 = strong, evidence required)

| Lens | Score | Evidence (file:line, test, benchmark) | Action |
|------|-------|----------------------------------------|--------|
| Problem & forces | 4 | SPEC.md Problem/Scope name correctness, operability, and throughput as dominant forces | Keep operator status and duplicate safety as first-class ACs |
| Alternatives & tradeoffs | 3 | SPEC.md Decisions list modular monolith vs. deployables and Kafka/Redpanda/Event Hubs | Keep broker/runtime choices behind config; revisit before production |
| Domain fit (DDD) | 3 | SPEC.md frames telemetry as an integration backbone, not a rich domain model yet | Introduce equipment/station aggregates in the next feature spec |
| Architecture & boundaries | 4 | SPEC.md Decisions separate API acceptance from worker consumption and own Postgres state | Preserve ports/adapters so broker and auth can be swapped |
| Testable design | 4 | SPEC.md ACs map to observable API, persistence, worker, dead-letter, and health scenarios | Cite scenario names in tests during implementation |
| Craftsmanship | 4 | SPEC.md chooses the smallest reliable baseline: API key, outbox-style durability, bounded retries | Avoid speculative abstractions until a second consumer exists |
| Decision record | 4 | SPEC.md records context, options, consequences, and reversibility for five decisions | Add revisit triggers when production constraints appear |

## Verdict (agent fills)

```
VERDICT: ACCEPT
STEELMAN: Separate .NET API and worker deployables over a Kafka-compatible topic with Postgres-backed acceptance, idempotent processing, and dead-letter evidence is the strongest first backbone because it is observable, replayable, and reversible enough for a greenfield product.
SMALLEST NEXT STEP: Implement wave 1 behind interfaces for auth, validation, and event publication so later waves can prove persistence and consumer idempotency without changing the public contract.
REVISIT TRIGGER: Reopen if production requirements demand OIDC/mTLS, managed broker constraints, throughput targets beyond single-node Redpanda, or a second consumer that makes the worker boundary wrong.
```

Run the review with `/challenge-decision` and the `decision-challenger`
subagent. It is advisory: it never blocks a gate or a merge.
