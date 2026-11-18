# Waves — ridership-analytics

Each wave IS an iteration: a time-boxed, independently shippable
increment — one branch, one PR, green tests, demoable outcome.
Do not start wave N+1 until wave N merges.

## Wave 1 — iteration 1

Scenarios proven by this wave:
- Telemetry events are consumed and stored as ridership readings
- Redelivered events do not double-count readings
- Unparseable or irrelevant events are handled without crashing
- Analytics service reports health

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US1 | As a platform operator, I want telemetry events consumed into durable ridership readings, so that analytics has data to serve | M | — | analytics consumer group on equipment.telemetry.v1, Postgres schema, idempotent processed-marking, health probes |

## Wave 2 — iteration 2

Scenarios proven by this wave:
- Counts are queryable by station and equipment over a time range
- Per-bucket time series are returned for trend inspection
- Queries without a valid API key are rejected
- Empty windows return zero counts, not errors

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US2 | As an operations analyst, I want to query ridership counts and time series, so that I can answer throughput questions | M | US1 | windowed rollups, count + bucketed-series endpoints, API-key auth, zero-count empty windows |

## Wave 3 — iteration 3

Scenarios proven by this wave:
- Service runs in the compose stack alongside existing services

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US3 | As a platform operator, I want the analytics service wired into the compose stack, so that it runs alongside the other services | S | US2 | Dockerfile, compose service with Postgres + Redpanda deps, smoke path |
