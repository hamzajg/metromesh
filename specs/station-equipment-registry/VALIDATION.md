# Validation — station-equipment-registry

## Traceability

| AC | Criterion | Scenarios | Wave(s) | Test evidence |
|----|-----------|-----------|---------|---------------|
| 1 | Operator can register a station and retrieve it by id | Invalid registry payload is rejected, Operator registers a station, Operator registers equipment against a station, Registry request without valid API key is rejected | 1 | RegistryEndpointTests.cs, RegistryPersistenceTests.cs |
| 2 | Operator can register turnstile and camera equipment… | Invalid registry payload is rejected, Operator registers a station, Operator registers equipment against a station, Registry request without valid API key is rejected | 1 | RegistryEndpointTests.cs, RegistryPersistenceTests.cs |
| 3 | Invalid registration payloads return ProblemDetails… | Invalid registry payload is rejected, Operator registers a station, Operator registers equipment against a station, Registry request without valid API key is rejected | 1 | RegistryEndpointTests.cs, RegistryPersistenceTests.cs |
| 4 | Requests without a valid API key are rejected | Invalid registry payload is rejected, Operator registers a station, Operator registers equipment against a station, Registry request without valid API key is rejected | 1 | RegistryEndpointTests.cs, RegistryPersistenceTests.cs |
| 5 | Equipment can be decommissioned and station closure… | Equipment status and station inventory are queryable, Station closure cascades to equipment decommission | 2 | RegistryEndpointTests.cs |
| 6 | Equipment status and station inventory are queryable | Equipment status and station inventory are queryable, Station closure cascades to equipment decommission | 2 | RegistryEndpointTests.cs |
| 7 | Registry mutations emit domain events to the event backbone | Registry mutation emits a domain event, Registry service reports health in compose stack | 3 | RegistryEndpointTests.cs, RegistryEventTests.cs |
| 8 | Service exposes live/readiness health and runs in the… | Registry mutation emits a domain event, Registry service reports health in compose stack | 3 | RegistryEndpointTests.cs, RegistryEventTests.cs |

## Scenario coverage

- Scenarios: 8 total, 8 with detected test evidence, 0 without
- ✓ `Operator registers a station` (@w1)
- ✓ `Operator registers equipment against a station` (@w1)
- ✓ `Invalid registry payload is rejected` (@w1)
- ✓ `Registry request without valid API key is rejected` (@w1)
- ✓ `Station closure cascades to equipment decommission` (@w2)
- ✓ `Equipment status and station inventory are queryable` (@w2)
- ✓ `Registry mutation emits a domain event` (@w3)
- ✓ `Registry service reports health in compose stack` (@w3)

## Demo script (manual walkthrough)

1. **Operator registers a station** — run the app, perform the scenario, observe the outcome.

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