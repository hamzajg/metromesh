# Validation: Platform resilience and observability

Validation performed after all waves complete.

## Method

- `dotnet build` — solution builds with no warnings as errors.
- `dotnet test` — all test projects pass (stdlib-only where CI runs
  `--no-deps`; integration tests need Docker).
- `docker compose up --build` — local smoke path starts all 7 services plus
  Postgres and Redpanda.
- `curl http://localhost:<port>/metrics` — each HTTP service returns
  OpenTelemetry counters.

## Evidence

| # | Criterion | Scenario | Wave | Evidence |
|---|-----------|----------|------|----------|
| 1 | `/metrics` returns OpenTelemetry counters | Service exposes Prometheus metrics endpoint | 1 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |
| 2 | Correlation ID in Kafka envelope | Correlation ID propagates from HTTP request into Kafka event envelope | 2 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |
| 3 | Causation ID chains producer to consumer | Causation ID chains producer through Kafka to consumer | 2 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |
| 4 | Circuit breaker trips with fallback | Circuit breaker trips when downstream dependency is unavailable | 3 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |
| 5 | Bounded retries before dead-lettering | Bounded retries before dead-lettering | 4 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |
| 6 | Shared resilience abstractions in Telemetry.Core | All services share resilience abstractions from Telemetry.Core | 4 | `dotnet build` 0 errors; `dotnet test` Worker 4/4, Ingest 6/6, Registry 11/11, Maintenance 16/16, Analytics 11/11 |

## Result

PASS — validation performed after wave 4; build green (0 errors), all integration tests passing.
