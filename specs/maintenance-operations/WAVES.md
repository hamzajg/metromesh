# Waves — maintenance-operations

Each wave IS an iteration: a time-boxed, independently shippable
increment — one branch, one PR, green tests, demoable outcome.
Do not start wave N+1 until wave N merges.

## Wave 1 — iteration 1

Scenarios proven by this wave:
- Planner raises a maintenance request
- Planner creates a work order for equipment
- Invalid maintenance payload is rejected
- Maintenance request without valid API key is rejected

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US1 | As a maintenance planner, I want to raise requests and create work orders, so that faults become tracked work | M | — | request + work-order intake, validation, API-key auth, registry equipment check, Postgres persistence |

## Wave 2 — iteration 2

Scenarios proven by this wave:
- Work order lifecycle transitions are recorded
- Work against invalid equipment is rejected
- Maintenance status and history are queryable

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US2 | As a technician, I want to work an assigned order through its lifecycle, so that progress and history are auditable | M | US1 | assign/start/complete/cancel transitions, history records, status + history queries |

## Wave 3 — iteration 3

Scenarios proven by this wave:
- Maintenance mutation emits a domain event
- Maintenance service reports health in compose stack

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US3 | As a platform operator, I want maintenance domain events on the backbone, so that notifications and analytics can react | M | US2 | outbox publisher to maintenance.workorder.v1, health probes, compose wiring |
