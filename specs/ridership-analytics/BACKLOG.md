# Backlog: ridership-analytics

Items are USER STORIES in the parseable format below — keep the heading
shape `## US<n> — <story>` and the field lines intact so `wave_plan` can
parse them. Write the story as: "As a <role>, I want <goal>, so that
<value>" — the title after the dash IS the story.

```
## US1 — As a platform operator, I want telemetry events consumed into durable ridership readings, so that analytics has data to serve
- Scenarios: @w1
- Size: M
- Notes: analytics consumer group on equipment.telemetry.v1, Postgres schema, idempotent processed-marking, health probes

## US2 — As an operations analyst, I want to query ridership counts and time series, so that I can answer throughput questions
- Scenarios: @w2
- Depends: US1
- Size: M
- Notes: windowed rollups, count + bucketed-series endpoints, API-key auth, zero-count empty windows

## US3 — As a platform operator, I want the analytics service wired into the compose stack, so that it runs alongside the other services
- Scenarios: @w3
- Depends: US2
- Size: S
- Notes: Dockerfile, compose service with Postgres + Redpanda deps, smoke path
```
