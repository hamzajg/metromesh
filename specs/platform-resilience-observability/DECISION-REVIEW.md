# Decision review: Platform resilience and observability

## Decision context

The platform resilience and observability feature is cross-cutting across all
seven MetroMesh deployables. The key architectural decisions are:

1. **Observability stack**: OpenTelemetry (metrics + tracing) rather than
   Azure Application Insights, because OpenTelemetry is vendor-neutral, local
   exporters (console, Prometheus scraping) require no cloud account, and the
   ebook's chapter 13 recommends OpenTelemetry for platform-agnostic
   telemetry.

2. **Shared abstractions in Telemetry.Core**: `ICircuitBreakerPolicy`,
   `IResilientHttpClient`, and `ITelemetryMetrics` live in the shared core
   library so all services reuse the same patterns, consistent with the
   existing `IEventBus`/`ITelemetryEventStore` pattern.

3. **Circuit breaker implementation**: Polly-style policy interface wrapped
   behind `ICircuitBreakerPolicy` to avoid a direct Polly dependency in
   service projects; the core library owns the policy contract, service
   projects configure it.

4. **Correlation/causation propagation**: Correlation ID from `X-Correlation-Id`
   HTTP header becomes the OpenTelemetry operation ID; causation ID becomes
   the parent ID. Kafka event envelopes carry both IDs so consumers can
   continue the trace.

## Alternatives considered

- Azure Application Insights: rejected for MVP — requires cloud account and
  connection string; OpenTelemetry achieves the same three pillars locally.
- Direct Polly usage in each service: rejected — would duplicate
  configuration across seven services; shared core abstraction is cleaner.
- Separate correlation ID generation vs. propagating incoming header: incoming
  header wins (with fallback to new GUID) to support upstream trace
  continuation per ebook chapter 7.

## Tradeoffs

- OpenTelemetry .NET SDK adds dependency surface to all projects; mitigated by
  centralizing configuration in Telemetry.Core extension methods.
- Circuit breaker state is in-memory per instance, not distributed; acceptable
  for MVP — distributed breaker state is a production concern.

## Decision

Use OpenTelemetry metrics (Prometheus scraping endpoint) and tracing
(console exporter locally), with correlation ID as operation ID and causation
ID as parent ID, propagated through HTTP headers and Kafka envelopes. Shared
resilience abstractions (`ICircuitBreakerPolicy`, `IResilientHttpClient`,
`ITelemetryMetrics`) live in `MetroMesh.Telemetry.Core`.
