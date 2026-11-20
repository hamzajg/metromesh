# Backlog: passenger-loyalty

```
## US1 — As an operations staff member, I want to create and update passenger profiles, so that passengers have durable identity records
- Scenarios: @w1
- Size: M
- Notes: passenger profile CRUD, passenger info updates with change history, validation, API-key auth, Postgres persistence

## US2 — As an operations staff member, I want to manage loyalty programs and notify on changes, so that loyalty state is tracked and passengers are informed
- Scenarios: @w2
- Depends: US1
- Size: M
- Notes: loyalty program CRUD, retirement, change notifications, audit trail

## US3 — As a platform operator, I want passenger-loyalty domain events on the backbone, so that downstream consumers react to passenger/loyalty lifecycle changes
- Scenarios: @w3
- Depends: US2
- Size: M
- Notes: outbox publisher to passenger.loyalty.v1, health probes, compose wiring
```
