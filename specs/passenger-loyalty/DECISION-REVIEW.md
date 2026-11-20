# Decision review brief — Spec: passenger-loyalty

- Source: `specs/passenger-loyalty/SPEC.md (## Decisions)`
- Generated: 2026-11-20T09:55:00Z
- Detected type: architecture, data
- Readiness: **CHALLENGE** (6/6 record elements)
- This brief is input to judgment, not a verdict. The agent fills the scorecard after reading the code.

**Reversibility:** stated

**Alternatives on record (verify they are credible, not strawmen):**
- Options: (a) new MetroMesh.Passenger.Loyalty service with its own consumer group, Postgres schema, and query API; (b) extend the registry API to handle passenger/loyalty
- Options: (a) change notification at write + domain events; (b) poll-based change feed (rejected — higher latency); (c) synchronous notification send (rejected — loses durability)
- Options: (a) reuse processed-event marking + bounded-retry from MetroMesh.Telemetry.Worker; (b) rely on rollup upserts keyed by event id (rejected — not a rollup context)
- Options: (a) trust event payload ids; (b) validate against registry at consume time (rejected — coupling and latency); (c) denormalize station names via registry

## Evidence signals (heuristic — verify before acting)

- no code paths referenced — the review is document-only

## Design moves to test

- Can this be a module inside the current deployable before it becomes a service?
- Which dependency can be inverted so the core owns the interface?
- What is the smallest reversible step that tests the boundary?
- Which invariant spans more than one aggregate, and does the transaction span it too?
- Can the write model be separated from read shapes without splitting the database?
- What is the migration and rollback path for existing data?

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
- Which consistency guarantee is required, and which can be eventual?
**Good looks like:** Ubiquitous language is explicit, invariants are protected at the boundary, external models are translated.

### 4. Architecture & boundaries
- Which direction do dependencies point? Can the core be tested without the framework, DB, or network?
- What is the smallest boundary that isolates this change (module, port, package)?
- What happens when each external dependency is slow, down, or returns garbage?
- Where does data ownership live, and is there a second writer?
- What is the operational story: config, migrations, rollout, rollback, observability?
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
| Problem & forces | 5 | SPEC.md Problem/Outcome name the missing passenger/loyalty domain and do-nothing cost (untracked passenger identity, unenforced loyalty state); dossier cited as evidence, not invention | none |
| Alternatives & tradeoffs | 4 | SPEC.md Decisions record credible options incl. simplest (extend registry) and most reversible (new service w/ shared Core); poll-feed and sync-notify rejected with reasons | none |
| Domain fit (DDD) | 4 | Passenger/loyalty is a distinct bounded context with PII/notification invariants; no aggregate spanning contexts; decision records orphan-id acceptance | none |
| Architecture & boundaries | 4 | Consumer/API/DB behind ports mirroring proven worker+ingest patterns; failure modes named (broker down -> consumer lag, PG down -> 503) | none |
| Testable design | 4 | Scenarios map to Testcontainers Postgres+Redpanda tests per existing convention; time injected for change-history tests | none |
| Craftsmanship | 4 | Reuses processed-marking pattern; no speculative outbox/events; smallest reversible steps per wave | none |
| Decision record | 4 | ADR-style blocks with context/options/decision/consequences/reversibility; revisit trigger in verdict | none |

## Verdict (agent fills)

```
VERDICT: ACCEPT
STEELMAN: A separate passenger-loyalty service consuming passenger/loyalty events into Postgres with change notifications at write, audit trail, and domain events - the strongest form of the current design because it isolates passenger PII, preserves write/read separation, and reuses proven consumer/idempotency patterns.
SMALLEST NEXT STEP: Wave 1 delivers passenger profile CRUD + info updates + health only; defer loyalty programs and notifications until wave 2 proves the change shapes needed.
REVISIT TRIGGER: Reopen if fare/payment features are approved for MVP scope, or if notification channel requirements expand beyond API-based delivery.
```

Run the review with `/challenge-decision` and the `decision-challenger`
subagent. It is advisory: it never blocks a gate or a merge.
