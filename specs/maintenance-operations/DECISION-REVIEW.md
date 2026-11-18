# Decision review brief — Spec: maintenance-operations

- Source: `specs/maintenance-operations/SPEC.md (## Decisions)`
- Generated: 2026-11-18T01:34:48Z
- Detected type: architecture, integration
- Readiness: **CHALLENGE** (6/6 record elements)
- This brief is input to judgment, not a verdict. The agent fills the scorecard after reading the code.

**Reversibility:** stated

**Alternatives on record (verify they are credible, not strawmen):**
- Options: (a) new MetroMesh.Maintenance ASP.NET Core service sharing MetroMesh.Telemetry.Core; (b) extend the registry service (rejected — registry owns equipmen
- Options: (a) call registry GET /v1/equipment/{id} at intake with a short timeout; (b) replicate equipment state via consumed registry events (eventually consist
- Options: (a) emit maintenance.* events and let a future notifications service consume them; (b) implement webhook/email dispatch now.
- Alternatives: shared DB (rejected — violates bounded-context ownership), synchronous publish (rejected — loses durability).

## Evidence signals (heuristic — verify before acting)

- no code paths referenced — the review is document-only

## Design moves to test

- Can this be a module inside the current deployable before it becomes a service?
- Which dependency can be inverted so the core owns the interface?
- What is the smallest reversible step that tests the boundary?
- Is an anti-corruption layer needed, or will the external model leak inward?
- What happens on timeout, duplicate delivery, partial failure, and replay?
- Can this be async where the business allows it, removing temporal coupling?
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
| Problem & forces | 5 | SPEC.md Problem/Outcome; dossier FR-05 | |
| Alternatives & tradeoffs | 4 | SPEC.md Decisions: 3 options per decision | |
| Domain fit (DDD) | 5 | bounded context = maintenance; aggregates request/work-order | |
| Architecture & boundaries | 4 | new service + shared Core; own schema/outbox; registry read-only dependency | |
| Testable design | 4 | same test seams as registry (WebApplicationFactory, Testcontainers, fake registry client) | |
| Craftsmanship | 4 | reuses proven outbox/idempotency patterns; synchronous registry check is the smallest reversible coupling | |
| Decision record | 5 | context/options/decision/consequences/reversibility per decision | |

## Verdict (agent fills)

```
VERDICT: ACCEPT
STEELMAN: Separate maintenance bounded-context service with Postgres+outbox,
synchronous read-only equipment validation via the registry API, and
events-only notification posture is the strongest fit — it mirrors the two
proven service patterns and keeps every external dependency behind a
reversible seam.
SMALLEST NEXT STEP: Implement the registry equipment check as an
IEquipmentDirectory port with an HTTP adapter so tests fake it and a later
event-driven projection can replace it without contract changes.
REVISIT TRIGGER: registry availability becomes a measured intake bottleneck
(move to event-replicated equipment state), or notification delivery is
scheduled (add dispatcher consuming maintenance.* events).
```

Run the review with `/challenge-decision` and the `decision-challenger`
subagent. It is advisory: it never blocks a gate or a merge.




