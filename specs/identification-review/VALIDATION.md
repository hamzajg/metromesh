# Validation — identification-review

## Traceability

| AC | Criterion | Scenarios | Wave(s) | Test evidence |
|----|-----------|-----------|---------|---------------|
| 1 | Identification results are ingested and stored as customer… | Identification request without valid API key is rejected, Identification result is ingested and stored as a station interaction, Invalid identification payload is rejected, Offender identification is ingested and stored as a station interaction | 1 | (weak) AnalyticsEndpointTests.cs, IdentificationEndpointTests.cs |
| 2 | Invalid identification payloads return ProblemDetails… | Identification request without valid API key is rejected, Identification result is ingested and stored as a station interaction, Invalid identification payload is rejected, Offender identification is ingested and stored as a station interaction | 1 | (weak) AnalyticsEndpointTests.cs, IdentificationEndpointTests.cs |
| 3 | Requests without a valid API key are rejected | Identification request without valid API key is rejected, Identification result is ingested and stored as a station interaction, Invalid identification payload is rejected, Offender identification is ingested and stored as a station interaction | 1 | (weak) AnalyticsEndpointTests.cs, IdentificationEndpointTests.cs |
| 4 | Confirmed offender matches trigger alert notifications | Confirmed offender match triggers an alert notification, Human review is required before adverse action, Non-match biometric PII is purged per retention policy, Review decisions and purge events are audited | 2 | (weak) IdentificationProcessingTests.cs |
| 5 | Non-match biometric/PII is purged per retention policy | Confirmed offender match triggers an alert notification, Human review is required before adverse action, Non-match biometric PII is purged per retention policy, Review decisions and purge events are audited | 2 | (weak) IdentificationProcessingTests.cs |
| 6 | Human review is required before adverse action on… | Confirmed offender match triggers an alert notification, Human review is required before adverse action, Non-match biometric PII is purged per retention policy, Review decisions and purge events are audited | 2 | (weak) IdentificationProcessingTests.cs |
| 7 | Identification mutations emit domain events to the event… | Identification mutation emits a domain event, Identification service reports health in compose stack | 3 | (weak) MaintenanceEndpointTests.cs, MaintenanceEventTests.cs |
| 8 | Service exposes live/readiness health and runs in the… | Identification mutation emits a domain event, Identification service reports health in compose stack | 3 | (weak) MaintenanceEndpointTests.cs, MaintenanceEventTests.cs |

## Scenario coverage

- Scenarios: 10 total, 0 with detected test evidence, 6 with weak (token-match) evidence only, 4 without
- ✗ `Identification result is ingested and stored as a station interaction` (@w1)
- ✗ `Offender identification is ingested and stored as a station interaction` (@w1)
- ~ `Invalid identification payload is rejected` (@w1)
- ~ `Identification request without valid API key is rejected` (@w1)
- ~ `Confirmed offender match triggers an alert notification` (@w2)
- ~ `Non-match biometric PII is purged per retention policy` (@w2)
- ✗ `Human review is required before adverse action` (@w2)
- ✗ `Review decisions and purge events are audited` (@w2)
- ~ `Identification mutation emits a domain event` (@w3)
- ~ `Identification service reports health in compose stack` (@w3)

## Demo script (manual walkthrough)

1. **Identification result is ingested and stored as a station interaction** — run the app, perform the scenario, observe the outcome.

## Gaps

- 4 scenario(s) lack detected test evidence — verify manually or add checks

Note: 'no evidence' means neither the scenario name nor a
token match was found in test files — verify by hand before
treating it as a real gap. '~' marks weak token-match evidence.

## Outcome follow-up

Optional; this is a post-release learning record, not a finish gate.
- Status: not yet measured
- Owner and review date: (record when known)
- Signal and observed result: (record after release; do not invent)
- Learning or next action: (record after review)