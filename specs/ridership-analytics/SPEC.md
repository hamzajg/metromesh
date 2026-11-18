# Spec: ridership-analytics

> Spec-driven development artifact. This file is the contract: implementation
> is done when every acceptance criterion maps to a scenario in
> `EXAMPLES.feature` and every scenario has a passing check.

## Problem

Telemetry events flow through the backbone and stations/equipment are
registered, but there is no read side: operators cannot answer "how many
passengers used turnstile X at station Y in the last hour" or compare
station throughput. Raw events are durable but not aggregated, and the
ingest API is a write path — it must not become a query engine.

## Users

- Operations analysts querying ridership counts by station, equipment,
  and time window.
- Operations staff comparing station throughput and equipment utilization.
- Downstream consumers (dashboards, reports) needing stable read APIs.

## Outcome hypothesis

- Expected user outcome: ridership questions are answered from durable,
  incrementally maintained aggregates instead of raw event scans.
- Opportunity evidence: product dossier identifies ridership analytics as
  the read-model capability that consumes the telemetry backbone and
  station/equipment context.
- Signal: analytics query API coverage of count/aggregation scenarios; no
  numeric target set.
- Follow-up owner/date: product owner, after first operations dashboard
  integration.

## Scope (in)

- Kafka consumer on `equipment.telemetry.v1` in its own consumer group,
  writing ridership readings into a Postgres-owned analytics schema.
- Incremental windowed rollups (per station, per equipment, per time
  bucket) applied idempotently — redelivery must not double-count.
- Query API: ridership counts by station/equipment over a time range,
  plus per-bucket series for trend inspection.
- API-key auth, correlation IDs, ProblemDetails errors, health probes,
  Docker Compose wiring.

## Scope (out)

- Browser UI / dashboard rendering.
- Real-time streaming (SSE/WebSocket) — queries are request/response.
- Predictive analytics, anomaly detection, or ML models.
- Writing back into telemetry, registry, or maintenance domains.
- Notifications or alerting on ridership thresholds.

## Acceptance criteria

| # | Criterion | Scenario(s) |
|---|-----------|-------------|
| 1 | Telemetry events are consumed and stored as ridership readings | @w1 |
| 2 | Redelivered events do not double-count readings | @w1 |
| 3 | Unparseable or irrelevant events are handled without crashing the consumer | @w1 |
| 4 | Analytics service exposes live/readiness health | @w1 |
| 5 | Counts are queryable by station and equipment over a time range | @w2 |
| 6 | Per-bucket time series are returned for trend inspection | @w2 |
| 7 | Queries without a valid API key are rejected | @w2 |
| 8 | Empty windows return zero counts, not errors | @w2 |
| 9 | Service runs in the compose stack alongside existing services | @w3 |

## Decisions

- **Separate analytics bounded-context service.** Context & forces:
  ridership analytics is a read model over telemetry — a different
  consistency and scaling profile from the write-path ingest API and the
  generic worker. Options: (a) new `MetroMesh.Analytics` service with its
  own consumer group, Postgres schema, and query API; (b) extend the
  telemetry worker to also serve queries (rejected — worker is
  infrastructure, not a domain read API, and would couple consumer
  throughput to query load); (c) query the ingest store directly
  (rejected — violates write/read separation and scans raw events).
  Decision: (a). Consequences: + clean read-model boundary, independent
  scaling; − another deployable. Reversibility: two-way; can be folded
  into a future reporting service.
- **Incremental windowed rollups in Postgres.** Context & forces:
  queries must be cheap and bounded; raw event scans grow unboundedly.
  Options: (a) rollup tables keyed by (station, equipment, bucket)
  updated transactionally with idempotent event marking; (b) scan raw
  events per query (rejected — unbounded cost); (c) external time-series
  database (rejected — new infrastructure for marginal gain at this
  scale). Decision: (a). Consequences: + bounded query cost, durable;
  − rollup logic must be correct under redelivery (handled by
  processed-marking). Reversibility: two-way — rollups can be rebuilt
  from raw events.
- **Idempotent consumer, same pattern as the worker.** Context & forces:
  Kafka is at-least-once; rollups must not double-count. Options: (a)
  reuse the processed-event marking + bounded-retry approach from
  `MetroMesh.Telemetry.Worker`; (b) rely on rollup upserts keyed by event
  id (rejected — more complex, same guarantee). Decision: (a) —
  `analytics_processed` marker table, processed events skipped.
  Reversibility: two-way.
- **Event payload carries the join keys; no registry calls at consume
  time.** Context & forces: analytics keys on station/equipment ids that
  telemetry events already carry; calling the registry per event adds a
  hot-path dependency for data the event already contains. Options:
  (a) trust event payload ids; (b) validate against registry at consume
  time (rejected — coupling and latency); (c) denormalize station names
  via registry at query time (deferred — display concern, not analytics).
  Decision: (a). Consequences: + decoupled, fast consumption; − orphan
  ids possible if events reference unregistered equipment (accepted —
  analytics reflects what was measured, registry owns validity).
  Reversibility: two-way — can add enrichment later without contract
  changes.
- **Request/response queries only; no event publication.** Context &
  forces: analytics is a terminal read model; nothing downstream needs
  its output yet. Options: (a) no outbox, no domain events; (b) publish
  `analytics.rollup.*` events (rejected — speculative, no consumer).
  Decision: (a). Consequences: + simpler service; − future consumers
  would need retrofit (cheap — outbox pattern is proven). Reversibility:
  two-way.
- **Postgres-per-service.** Same durability model as the rest of
  MetroMesh: analytics tables in a Postgres database owned by the
  service. Alternatives: shared DB (rejected — bounded-context
  ownership), schema inside ingest DB (rejected — read model must not
  share the write path's failure domain). Reversibility: two-way.
