# Validation — passenger-loyalty

## Traceability

| AC | Criterion | Scenarios | Wave(s) | Test evidence |
|----|-----------|-----------|---------|---------------|
| 1 | Operator can create a passenger profile and retrieve it by… | Invalid passenger payload is rejected, Operator creates a passenger profile, Operator updates passenger profile info, Passenger request without valid API key is rejected | 1 | (weak) AnalyticsEndpointTests.cs, AnalyticsProcessingTests.cs |
| 2 | Passenger profile info can be updated with change history | Invalid passenger payload is rejected, Operator creates a passenger profile, Operator updates passenger profile info, Passenger request without valid API key is rejected | 1 | (weak) AnalyticsEndpointTests.cs, AnalyticsProcessingTests.cs |
| 3 | Invalid passenger/loyalty payloads return ProblemDetails… | Invalid passenger payload is rejected, Operator creates a passenger profile, Operator updates passenger profile info, Passenger request without valid API key is rejected | 1 | (weak) AnalyticsEndpointTests.cs, AnalyticsProcessingTests.cs |
| 4 | Requests without a valid API key are rejected | Invalid passenger payload is rejected, Operator creates a passenger profile, Operator updates passenger profile info, Passenger request without valid API key is rejected | 1 | (weak) AnalyticsEndpointTests.cs, AnalyticsProcessingTests.cs |
| 5 | Operator can create, update, and retire a loyalty program | Change notifications are audited, Operator creates a loyalty program for a passenger, Operator updates and retires a loyalty program, Profile/loyalty change triggers a notification | 2 | (weak) PassengerEndpointTests.cs, PassengerProcessingTests.cs |
| 6 | Profile/loyalty changes trigger notifications to configured… | Change notifications are audited, Operator creates a loyalty program for a passenger, Operator updates and retires a loyalty program, Profile/loyalty change triggers a notification | 2 | (weak) PassengerEndpointTests.cs, PassengerProcessingTests.cs |
| 7 | Passenger/loyalty mutations emit domain events to the event… | Passenger-loyalty service reports health in compose stack, Passenger/loyalty mutation emits a domain event | 3 | (weak) MaintenanceEndpointTests.cs, MaintenanceEventTests.cs |
| 8 | Service exposes live/readiness health and runs in the… | Passenger-loyalty service reports health in compose stack, Passenger/loyalty mutation emits a domain event | 3 | (weak) MaintenanceEndpointTests.cs, MaintenanceEventTests.cs |

## Scenario coverage

- Scenarios: 10 total, 0 with detected test evidence, 7 with weak (token-match) evidence only, 3 without
- ~ `Operator creates a passenger profile` (@w1)
- ✗ `Operator updates passenger profile info` (@w1)
- ~ `Invalid passenger payload is rejected` (@w1)
- ~ `Passenger request without valid API key is rejected` (@w1)
- ~ `Operator creates a loyalty program for a passenger` (@w2)
- ~ `Operator updates and retires a loyalty program` (@w2)
- ✗ `Profile/loyalty change triggers a notification` (@w2)
- ✗ `Change notifications are audited` (@w2)
- ~ `Passenger/loyalty mutation emits a domain event` (@w3)
- ~ `Passenger-loyalty service reports health in compose stack` (@w3)

## Demo script (manual walkthrough)

1. **Operator creates a passenger profile** — run the app, perform the scenario, observe the outcome.

## Gaps

- 3 scenario(s) lack detected test evidence — verify manually or add checks

Note: 'no evidence' means neither the scenario name nor a
token match was found in test files — verify by hand before
treating it as a real gap. '~' marks weak token-match evidence.

## Outcome follow-up

Optional; this is a post-release learning record, not a finish gate.
- Status: not yet measured
- Owner and review date: (record when known)
- Signal and observed result: (record after release; do not invent)
- Learning or next action: (record after review)