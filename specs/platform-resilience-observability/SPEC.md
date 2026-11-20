# Spec: Platform resilience and observability

> Spec-driven development artifact. This file is the contract: implementation
> is done when every acceptance criterion maps to a scenario in
> `EXAMPLES.feature` and every scenario has a passing check.

## Problem

MetroMesh services expose health probes and correlation IDs, but lack the
observability and resilience capabilities required for a production
event-driven platform. There are no application-level metrics, no distributed
tracing across event producers/consumers, no circuit breaking or bulkheading
for downstream calls, and no standardized edge event buffering pattern. Each
service repeats ad-hoc resilience logic instead of sharing a platform concern.

## Users

- Platform operators monitoring service health, consumer lag, and event
  failures across all seven deployables.
- Service developers needing correlation/causation traces to debug
  cross-service event flows.
- Incident responders requiring circuit breaker and fallback behavior when
  dependent services (e.g. registry from maintenance) are unavailable.

## Outcome hypothesis

- Expected user outcome: an operator can scrape metrics from each service,
  trace a single event from ingestion through worker processing using
  correlation/causation IDs, and observe graceful degradation when a
  dependency fails.
- Opportunity evidence: assumption based on the platform resilience and
  observability requirements in the product domain dossier.
- Signal: metrics endpoints return counters/traces; correlation IDs appear in
  logs across producer, Kafka, and consumer; circuit breaker trips when the
  registry is down.
- Follow-up owner/date: product owner reviews after the first deployed
  release.

## Scope (in)

- OpenTelemetry meters and counters per service (requests received, events
  published, events processed, dead-lettered, circuit breaker trips).
- Prometheus scraping endpoint (`/metrics`) on each HTTP service; console
  exporter for workers.
- OpenTelemetry tracing with correlation ID as operation ID and causation ID
  as parent ID propagated through HTTP headers and Kafka event envelopes.
- Circuit breaker, bulkhead, fallback, and timeout patterns for outgoing
  HTTP calls (e.g. maintenance -> registry directory).
- Shared resilience abstractions in `MetroMesh.Telemetry.Core` so all
  services reuse the same patterns.
- Edge event buffering: outbox + bounded retry + dead-letter pattern extended
  to all services with publish/failback semantics.
- Structured logs enriched with correlation ID on every service.

## Scope (out)

- Azure Application Insights or other cloud APM (OpenTelemetry exporters are
  platform-agnostic; the ops team selects the backend).
- Jaeger/Prometheus production deployment (local exporters and scraping
  endpoints only).
- mTLS, OAuth2/OIDC, fine-grained authorization beyond API key.
- Real hardware certification or vendor-specific gateway protocols.

## Acceptance criteria

| AC | Scenario (from EXAMPLES.feature) | Verified by |
|----|----------------------------------|-------------|
| 1 | Service exposes `/metrics` with OpenTelemetry counters | @w1 |
| 2 | Correlation ID propagated from HTTP request into Kafka event envelope | @w1 |
| 3 | Causation ID chains producer -> Kafka -> consumer logs | @w1 |
| 4 | Circuit breaker trips and returns fallback when downstream dependency fails | @w1 |
| 5 | Bounded retries with backoff before dead-lettering | @w1 |
| 6 | All services share resilience abstractions from `MetroMesh.Telemetry.Core` | @w1 |

## Related

- Feature 8 (`service-catalog-governance`) depends on health endpoints and
  OpenAPI specs present in this feature's services.
