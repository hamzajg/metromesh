# Waves — equipment-telemetry-backbone

Each wave IS an iteration: a time-boxed, independently shippable
increment — one branch, one PR, green tests, demoable outcome.
Do not start wave N+1 until wave N merges.

## Wave 1 — iteration 1

Scenarios proven by this wave:
- Gateway submits valid telemetry event
- Telemetry payload with missing required fields is rejected
- Telemetry request without valid API key is rejected

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US1 | As a gateway integrator, I want to submit validated telemetry through an authenticated API, so that only well-formed events enter the platform | M | — | CloudEvents-compatible JSON, API-key auth, ProblemDetails errors, correlation ids. |

## Wave 2 — iteration 2

Scenarios proven by this wave:
- Accepted telemetry event is persisted and published
- Telemetry event status can be queried

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US2 | As a platform operator, I want accepted telemetry durably stored and published for processing, so that events survive retries and can be inspected | L | US1 | PostgreSQL accepted-event/outbox records, Kafka API topic, status query endpoint. |

## Wave 3 — iteration 3

Scenarios proven by this wave:
- Worker records processed telemetry exactly once
- Duplicate telemetry delivery does not create duplicate processed record

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US3 | As a service developer, I want a worker to consume telemetry idempotently, so that downstream features receive each event once | L | US2 | Worker service, processed-event ledger, duplicate-delivery safety, structured logs. |

## Wave 4 — iteration 4

Scenarios proven by this wave:
- Poison telemetry event is dead-lettered after bounded retries
- Service health endpoints report readiness

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US4 | As an operations engineer, I want bounded retries, dead-letter evidence, health probes, and a local smoke path, so that failures are visible and recoverable | M | US3 | Dead-letter records, readiness checks, metrics/logging, Docker Compose smoke script. |
