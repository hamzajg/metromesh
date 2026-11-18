# Backlog: maintenance-operations

```
## US1 — As a maintenance planner, I want to raise requests and create work orders, so that faults become tracked work
- Scenarios: @w1
- Size: M
- Notes: request + work-order intake, validation, API-key auth, registry equipment check, Postgres persistence

## US2 — As a technician, I want to work an assigned order through its lifecycle, so that progress and history are auditable
- Scenarios: @w2
- Depends: US1
- Size: M
- Notes: assign/start/complete/cancel transitions, history records, status + history queries

## US3 — As a platform operator, I want maintenance domain events on the backbone, so that notifications and analytics can react
- Scenarios: @w3
- Depends: US2
- Size: M
- Notes: outbox publisher to maintenance.workorder.v1, health probes, compose wiring
```
