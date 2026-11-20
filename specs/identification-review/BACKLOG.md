# Backlog: identification-review

```
## US1 — As an operations analyst, I want identification results ingested as reviewable station interactions, so that I can see who passed through each station
- Scenarios: @w1
- Size: M
- Notes: identification ingestion, interaction records, privacy redaction, validation, API-key auth, Postgres persistence

## US2 — As a security reviewer, I want offender-match alerts, retention purge, and human-review gates, so that I can act on matches and comply with privacy policy
- Scenarios: @w2
- Depends: US1
- Size: M
- Notes: alert notifications, PII purge per retention, human-review gate, audit trail

## US3 — As a platform operator, I want identification domain events on the backbone, so that downstream consumers react to identification lifecycle changes
- Scenarios: @w3
- Depends: US2
- Size: M
- Notes: outbox publisher to identification.review.v1, health probes, compose wiring
```
