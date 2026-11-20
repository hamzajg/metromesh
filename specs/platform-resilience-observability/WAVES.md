# Waves — platform-resilience-observability

Each wave IS an iteration: a time-boxed, independently shippable
increment — one branch, one PR, green tests, demoable outcome.
Do not start wave N+1 until wave N merges.

## Wave 1 — iteration 1

Scenarios proven by this wave:
- Service exposes Prometheus metrics endpoint

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US1 | As a platform operator, I want each service to expose Prometheus metrics, so that I can scrape health and throughput signals | M | — | OpenTelemetry meters/counters per service, /metrics endpoint on HTTP services, console exporter on workers. |

## Wave 2 — iteration 2

Scenarios proven by this wave:
- Correlation ID propagates from HTTP request into Kafka event envelope
- Causation ID chains producer through Kafka to consumer

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US2 | As a service developer, I want correlation and causation IDs propagated through HTTP and Kafka, so that I can trace events across services | L | US1 | Correlation ID header -> event envelope -> consumer logging; causation ID as parent ID chain. |

## Wave 3 — iteration 3

Scenarios proven by this wave:
- Circuit breaker trips when downstream dependency is unavailable

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US3 | As an incident responder, I want circuit breakers and fallbacks for downstream HTTP calls, so that a failing dependency does not cascade failures | L | US2 | Shared ICircuitBreakerPolicy and IResilientHttpClient in MetroMesh.Telemetry.Core; maintenance -> registry calls protected. |

## Wave 4 — iteration 4

Scenarios proven by this wave:
- Bounded retries before dead-lettering
- All services share resilience abstractions from Telemetry.Core

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US4 | As an operations engineer, I want bounded retries with backoff and dead-lettering on all services, so that failures are visible and recoverable | M | US3 | Outbox + retry + dead-letter pattern extended to all services; publish/failback semantics. |
