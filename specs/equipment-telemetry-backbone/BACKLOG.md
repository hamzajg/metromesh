# Backlog: Equipment telemetry backbone

Items are USER STORIES in the parseable format below — keep the heading
shape `## US<n> — <story>` and the field lines intact so `wave_plan` can
parse them. Write the story as: "As a <role>, I want <goal>, so that
<value>" — the title after the dash IS the story.

Order stories by user value; dependencies express technical order only.

## US1 — As a gateway integrator, I want to submit validated telemetry through an authenticated API, so that only well-formed events enter the platform
- Scenarios: @w1
- Size: M
- Notes: CloudEvents-compatible JSON, API-key auth, ProblemDetails errors, correlation ids.

## US2 — As a platform operator, I want accepted telemetry durably stored and published for processing, so that events survive retries and can be inspected
- Scenarios: @w2
- Depends: US1
- Size: L
- Notes: PostgreSQL accepted-event/outbox records, Kafka API topic, status query endpoint.

## US3 — As a service developer, I want a worker to consume telemetry idempotently, so that downstream features receive each event once
- Scenarios: @w3
- Depends: US2
- Size: L
- Notes: Worker service, processed-event ledger, duplicate-delivery safety, structured logs.

## US4 — As an operations engineer, I want bounded retries, dead-letter evidence, health probes, and a local smoke path, so that failures are visible and recoverable
- Scenarios: @w4
- Depends: US3
- Size: M
- Notes: Dead-letter records, readiness checks, metrics/logging, Docker Compose smoke script.
