# Waves — passenger-loyalty

Each wave IS an iteration: a time-boxed, independently shippable
increment — one branch, one PR, green tests, demoable outcome.
Do not start wave N+1 until wave N merges.

## Wave 1 — iteration 1

Scenarios proven by this wave:
- Operator can create a passenger profile and retrieve it by id
- Passenger profile info can be updated with change history
- Invalid passenger/loyalty payloads return ProblemDetails validation errors
- Requests without a valid API key are rejected

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US1 | As an operations staff member, I want to create and update passenger profiles | M | — | passenger consumer group on passenger.loyalty.v1, Postgres schema, idempotent processed-marking, health probes |

## Wave 2 — iteration 2

Scenarios proven by this wave:
- Operator can create, update, and retire a loyalty program
- Profile/loyalty changes trigger notifications to configured recipients
- Change notifications are audited

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US2 | As an operations staff member, I want to manage loyalty programs and notify on changes | M | US1 | loyalty program CRUD, retirement, change notifications, audit trail |

## Wave 3 — iteration 3

Scenarios proven by this wave:
- Passenger/loyalty mutations emit domain events to the event backbone
- Service runs in the compose stack alongside existing services

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US3 | As a platform operator, I want the passenger-loyalty service wired into the compose stack | S | US2 | Dockerfile, compose service with Postgres + Redpanda deps, smoke path |
