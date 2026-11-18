# Backlog: station-equipment-registry

```
## US1 — As an operations engineer, I want to register stations and equipment, so that telemetry has authoritative context
- Scenarios: @w1
- Size: M
- Notes: station + turnstile/camera registration, validation, API-key auth, Postgres persistence

## US2 — As an operations engineer, I want lifecycle transitions and inventory queries, so that I can decommission safely and audit status
- Scenarios: @w2
- Depends: US1
- Size: M
- Notes: install/decommission, station-close cascade, status + inventory queries

## US3 — As a platform operator, I want registry domain events on the backbone, so that downstream consumers react to lifecycle changes
- Scenarios: @w3
- Depends: US2
- Size: M
- Notes: outbox publisher to equipment.registry.v1, health probes, compose wiring
```
