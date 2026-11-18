# Waves — station-equipment-registry

Each wave IS an iteration: a time-boxed, independently shippable
increment — one branch, one PR, green tests, demoable outcome.
Do not start wave N+1 until wave N merges.

## Wave 1 — iteration 1

Scenarios proven by this wave:
- Operator registers a station
- Operator registers equipment against a station
- Invalid registry payload is rejected
- Registry request without valid API key is rejected

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US1 | As an operations engineer, I want to register stations and equipment, so that telemetry has authoritative context | M | — | station + turnstile/camera registration, validation, API-key auth, Postgres persistence |

## Wave 2 — iteration 2

Scenarios proven by this wave:
- Station closure cascades to equipment decommission
- Equipment status and station inventory are queryable

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US2 | As an operations engineer, I want lifecycle transitions and inventory queries, so that I can decommission safely and audit status | M | US1 | install/decommission, station-close cascade, status + inventory queries |

## Wave 3 — iteration 3

Scenarios proven by this wave:
- Registry mutation emits a domain event
- Registry service reports health in compose stack

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US3 | As a platform operator, I want registry domain events on the backbone, so that downstream consumers react to lifecycle changes | M | US2 | outbox publisher to equipment.registry.v1, health probes, compose wiring |
