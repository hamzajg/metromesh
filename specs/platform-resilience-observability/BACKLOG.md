# Backlog: Platform resilience and observability

Items are USER STORIES in the parseable format below — keep the heading
shape `## US<n> — <story>` and the field lines intact so `wave_plan` can
parse them. Write the story as: "As a <role>, I want <goal>, so that
<value>" — the title after the dash IS the story.

Order stories by user value; dependencies express technical order only.

## US1 — As a platform operator, I want each service to expose Prometheus metrics, so that I can scrape health and throughput signals
- Scenarios: @w1
- Size: M
- Notes: OpenTelemetry meters/counters per service, /metrics endpoint on HTTP services, console exporter on workers.

## US2 — As a service developer, I want correlation and causation IDs propagated through HTTP and Kafka, so that I can trace events across services
- Scenarios: @w2
- Depends: US1
- Size: L
- Notes: Correlation ID header -> event envelope -> consumer logging; causation ID as parent ID chain.

## US3 — As an incident responder, I want circuit breakers and fallbacks for downstream HTTP calls, so that a failing dependency does not cascade failures
- Scenarios: @w3
- Depends: US2
- Size: L
- Notes: Shared ICircuitBreakerPolicy and IResilientHttpClient in MetroMesh.Telemetry.Core; maintenance -> registry calls protected.

## US4 — As an operations engineer, I want bounded retries with backoff and dead-lettering on all services, so that failures are visible and recoverable
- Scenarios: @w4
- Depends: US3
- Size: M
- Notes: Outbox + retry + dead-letter pattern extended to all services; publish/failback semantics.
