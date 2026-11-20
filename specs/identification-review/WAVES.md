# Waves — identification-review

Each wave IS an iteration: a time-boxed, independently shippable
increment — one branch, one PR, green tests, demoable outcome.
Do not start wave N+1 until wave N merges.

## Wave 1 — iteration 1

Scenarios proven by this wave:
- Identification results are ingested and stored as customer station interactions
- Invalid identification payloads return ProblemDetails validation errors
- Requests without a valid API key are rejected
- Service exposes live/readiness health

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US1 | As an operations analyst, I want identification results ingested as reviewable station interactions | M | — | identification consumer group on identification.review.v1, Postgres schema, idempotent processed-marking, privacy redaction, health probes |

## Wave 2 — iteration 2

Scenarios proven by this wave:
- Confirmed offender matches trigger alert notifications
- Non-match biometric/PII is purged per retention policy
- Human review is required before adverse action on identifications
- Review decisions and purge events are audited

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US2 | As a security reviewer, I want offender-match alerts, retention purge, and human-review gates | M | US1 | alert notifications, retention purge job, human-review gate, audit trail entries |

## Wave 3 — iteration 3

Scenarios proven by this wave:
- Identification mutations emit domain events to the event backbone
- Service runs in the compose stack alongside existing services

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US3 | As a platform operator, I want the identification service wired into the compose stack | S | US2 | Dockerfile, compose service with Postgres + Redpanda deps, smoke path |
