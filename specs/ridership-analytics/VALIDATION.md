# Validation — ridership-analytics

## Traceability

| AC | Criterion | Scenarios | Wave(s) | Test evidence |
|----|-----------|-----------|---------|---------------|
| 1 | Telemetry events are consumed and stored as ridership… | Analytics service reports health, Redelivered events do not double-count readings, Telemetry events are consumed and stored as ridership readings, Unparseable or irrelevant events are handled without crashing | 1 | (weak) AnalyticsEndpointTests.cs, AnalyticsProcessingTests.cs |
| 2 | Redelivered events do not double-count readings | Analytics service reports health, Redelivered events do not double-count readings, Telemetry events are consumed and stored as ridership readings, Unparseable or irrelevant events are handled without crashing | 1 | (weak) AnalyticsEndpointTests.cs, AnalyticsProcessingTests.cs |
| 3 | Unparseable or irrelevant events are handled without… | Analytics service reports health, Redelivered events do not double-count readings, Telemetry events are consumed and stored as ridership readings, Unparseable or irrelevant events are handled without crashing | 1 | (weak) AnalyticsEndpointTests.cs, AnalyticsProcessingTests.cs |
| 4 | Analytics service exposes live/readiness health | Analytics service reports health, Redelivered events do not double-count readings, Telemetry events are consumed and stored as ridership readings, Unparseable or irrelevant events are handled without crashing | 1 | (weak) AnalyticsEndpointTests.cs, AnalyticsProcessingTests.cs |
| 5 | Counts are queryable by station and equipment over a time… | Counts are queryable by station and equipment over a time range, Empty windows return zero counts, not errors, Per-bucket time series are returned for trend inspection, Queries without a valid API key are rejected | 2 | (weak) AnalyticsEndpointTests.cs, MaintenanceEndpointTests.cs |
| 6 | Per-bucket time series are returned for trend inspection | Counts are queryable by station and equipment over a time range, Empty windows return zero counts, not errors, Per-bucket time series are returned for trend inspection, Queries without a valid API key are rejected | 2 | (weak) AnalyticsEndpointTests.cs, MaintenanceEndpointTests.cs |
| 7 | Queries without a valid API key are rejected | Counts are queryable by station and equipment over a time range, Empty windows return zero counts, not errors, Per-bucket time series are returned for trend inspection, Queries without a valid API key are rejected | 2 | (weak) AnalyticsEndpointTests.cs, MaintenanceEndpointTests.cs |
| 8 | Empty windows return zero counts, not errors | Counts are queryable by station and equipment over a time range, Empty windows return zero counts, not errors, Per-bucket time series are returned for trend inspection, Queries without a valid API key are rejected | 2 | (weak) AnalyticsEndpointTests.cs, MaintenanceEndpointTests.cs |
| 9 | Service runs in the compose stack alongside existing… | Service runs in the compose stack alongside existing services | 3 | — |

## Scenario coverage

- Scenarios: 9 total, 0 with detected test evidence, 3 with weak (token-match) evidence only, 6 without
- ✗ `Telemetry events are consumed and stored as ridership readings` (@w1)
- ✗ `Redelivered events do not double-count readings` (@w1)
- ✗ `Unparseable or irrelevant events are handled without crashing` (@w1)
- ~ `Analytics service reports health` (@w1)
- ✗ `Counts are queryable by station and equipment over a time range` (@w2)
- ✗ `Per-bucket time series are returned for trend inspection` (@w2)
- ~ `Queries without a valid API key are rejected` (@w2)
- ~ `Empty windows return zero counts, not errors` (@w2)
- ✗ `Service runs in the compose stack alongside existing services` (@w3)

## Demo script (manual walkthrough)

1. **Telemetry events are consumed and stored as ridership readings** — run the app, perform the scenario, observe the outcome.

## Gaps

- 6 scenario(s) lack detected test evidence — verify manually or add checks

Note: 'no evidence' means neither the scenario name nor a
token match was found in test files — verify by hand before
treating it as a real gap. '~' marks weak token-match evidence.

## Outcome follow-up

Optional; this is a post-release learning record, not a finish gate.
- Status: not yet measured
- Owner and review date: (record when known)
- Signal and observed result: (record after release; do not invent)
- Learning or next action: (record after review)