# Validation — equipment-telemetry-backbone

## Traceability

| AC | Criterion | Scenarios | Wave(s) | Test evidence |
|----|-----------|-----------|---------|---------------|
| 1 | API accepts a valid CloudEvents-compatible telemetry event… | Gateway submits valid telemetry event, Telemetry payload with missing required fields is rejected, Telemetry request without valid API key is rejected | 1 | TelemetryEndpointTests.cs |
| 2 | Missing required fields or invalid payload shape returns… | Gateway submits valid telemetry event, Telemetry payload with missing required fields is rejected, Telemetry request without valid API key is rejected | 1 | TelemetryEndpointTests.cs |
| 3 | Requests without a valid API key are rejected before… | Gateway submits valid telemetry event, Telemetry payload with missing required fields is rejected, Telemetry request without valid API key is rejected | 1 | TelemetryEndpointTests.cs |
| 4 | Accepted events are durably persisted and published to… | Accepted telemetry event is persisted and published, Telemetry event status can be queried | 2 | TelemetryPersistenceTests.cs |
| 5 | Operators can query event status by id across accepted… | Accepted telemetry event is persisted and published, Telemetry event status can be queried | 2 | TelemetryPersistenceTests.cs |
| 6 | The worker records processed telemetry exactly once per… | Duplicate telemetry delivery does not create duplicate processed record, Worker records processed telemetry exactly once | 3 | TelemetryProcessingTests.cs |
| 7 | Duplicate broker delivery does not create duplicate… | Duplicate telemetry delivery does not create duplicate processed record, Worker records processed telemetry exactly once | 3 | TelemetryProcessingTests.cs |
| 8 | An unprocessable event is moved to dead-letter state after… | Poison telemetry event is dead-lettered after bounded retries, Service health endpoints report readiness | 4 | HealthEndpointTests.cs, TelemetryDeadLetterTests.cs |
| 9 | API and worker expose live/readiness health suitable for… | Poison telemetry event is dead-lettered after bounded retries, Service health endpoints report readiness | 4 | HealthEndpointTests.cs, TelemetryDeadLetterTests.cs |

## Scenario coverage

- Scenarios: 9 total, 9 with detected test evidence, 0 without
- ✓ `Gateway submits valid telemetry event` (@w1)
- ✓ `Telemetry payload with missing required fields is rejected` (@w1)
- ✓ `Telemetry request without valid API key is rejected` (@w1)
- ✓ `Accepted telemetry event is persisted and published` (@w2)
- ✓ `Telemetry event status can be queried` (@w2)
- ✓ `Worker records processed telemetry exactly once` (@w3)
- ✓ `Duplicate telemetry delivery does not create duplicate processed record` (@w3)
- ✓ `Poison telemetry event is dead-lettered after bounded retries` (@w4)
- ✓ `Service health endpoints report readiness` (@w4)

## Demo script (manual walkthrough)

1. **Gateway submits valid telemetry event** — run the app, perform the scenario, observe the outcome.
2. **Worker records processed telemetry exactly once** — run the app, perform the scenario, observe the outcome.

## Gaps

- none — spec is fully traced

Note: 'no evidence' means neither the scenario name nor a
token match was found in test files — verify by hand before
treating it as a real gap. '~' marks weak token-match evidence.

## Outcome follow-up

Optional; this is a post-release learning record, not a finish gate.
- Status: not yet measured
- Owner and review date: (record when known)
- Signal and observed result: (record after release; do not invent)
- Learning or next action: (record after review)