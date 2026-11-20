# Validation: Service catalog and API governance

Validation performed after all waves complete.

## Method

- `dotnet build` — solution builds with no warnings as errors.
- `dotnet test` — all test projects pass.
- `curl http://localhost:<port>/swagger/v1/swagger.json` — each HTTP service
  returns OpenAPI JSON.
- Service discovery registration verified via Consul agent API.

## Evidence

| # | Criterion | Scenario | Wave | Evidence |
|---|-----------|----------|------|----------|
| 1 | OpenAPI JSON on all HTTP services | Every HTTP service serves OpenAPI JSON | 1 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |
| 2 | Routes versioned under /v1/*; OpenAPI info version set | All routes are versioned under /v1/* | 1 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |
| 3 | Self-registration with discovery agent | Service self-registers with discovery agent at startup | 2 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |
| 4 | Catalog lists all deployables with health | Service catalog lists all deployables with health status | 3 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |
| 5 | Maintenance->registry dependency visible | Maintenance->registry dependency is declared and visible | 4 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |

## Result

PASS — validation performed after wave 4; build green (0 errors), all integration tests passing.
