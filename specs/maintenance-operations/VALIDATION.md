# Validation — maintenance-operations

## Traceability

| AC | Criterion | Scenarios | Wave(s) | Test evidence |
|----|-----------|-----------|---------|---------------|
| 1 | Planner can raise a maintenance request for registered… | Invalid maintenance payload is rejected, Maintenance request without valid API key is rejected, Planner creates a work order for equipment, Planner raises a maintenance request | 1 | MaintenanceEndpointTests.cs |
| 2 | Planner can create and retrieve a work order linked to… | Invalid maintenance payload is rejected, Maintenance request without valid API key is rejected, Planner creates a work order for equipment, Planner raises a maintenance request | 1 | MaintenanceEndpointTests.cs |
| 3 | Invalid payloads return ProblemDetails validation errors | Invalid maintenance payload is rejected, Maintenance request without valid API key is rejected, Planner creates a work order for equipment, Planner raises a maintenance request | 1 | MaintenanceEndpointTests.cs |
| 4 | Requests without a valid API key are rejected | Invalid maintenance payload is rejected, Maintenance request without valid API key is rejected, Planner creates a work order for equipment, Planner raises a maintenance request | 1 | MaintenanceEndpointTests.cs |
| 5 | Work order can be assigned, started, completed, or… | Maintenance status and history are queryable, Work against invalid equipment is rejected, Work order lifecycle transitions are recorded | 2 | MaintenanceEndpointTests.cs |
| 6 | Work against unknown or decommissioned equipment is rejected | Maintenance status and history are queryable, Work against invalid equipment is rejected, Work order lifecycle transitions are recorded | 2 | MaintenanceEndpointTests.cs |
| 7 | Work-order and request status/history are queryable | Maintenance status and history are queryable, Work against invalid equipment is rejected, Work order lifecycle transitions are recorded | 2 | MaintenanceEndpointTests.cs |
| 8 | Maintenance mutations emit domain events to the event… | Maintenance mutation emits a domain event, Maintenance service reports health in compose stack | 3 | MaintenanceEndpointTests.cs, MaintenanceEventTests.cs |
| 9 | Service exposes live/readiness health and runs in the… | Maintenance mutation emits a domain event, Maintenance service reports health in compose stack | 3 | MaintenanceEndpointTests.cs, MaintenanceEventTests.cs |

## Scenario coverage

- Scenarios: 9 total, 8 with detected test evidence, 1 with weak (token-match) evidence only, 0 without
- ✓ `Planner raises a maintenance request` (@w1)
- ✓ `Planner creates a work order for equipment` (@w1)
- ✓ `Invalid maintenance payload is rejected` (@w1)
- ✓ `Maintenance request without valid API key is rejected` (@w1)
- ✓ `Work order lifecycle transitions are recorded` (@w2)
- ~ `Work against invalid equipment is rejected` (@w2)
- ✓ `Maintenance status and history are queryable` (@w2)
- ✓ `Maintenance mutation emits a domain event` (@w3)
- ✓ `Maintenance service reports health in compose stack` (@w3)

## Demo script (manual walkthrough)

1. **Planner raises a maintenance request** — run the app, perform the scenario, observe the outcome.

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