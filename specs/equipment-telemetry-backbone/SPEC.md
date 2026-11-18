# Spec: Equipment telemetry backbone

> Spec-driven development artifact. This file is the contract: implementation
> is done when every acceptance criterion maps to a scenario in
> `EXAMPLES.feature` and every scenario has a passing check.

## Problem

MetroMesh needs a dependable foundation for high-volume equipment telemetry
before domain features such as station inventory, maintenance, analytics, and
identification can be built. Today there is no agreed ingestion contract, no
durable handoff between gateways and services, and no shared way to prove that
an event was accepted, processed once, or safely dead-lettered. Without that
foundation, each later feature would invent its own ingestion, retry, and
duplicate-handling rules.

## Users

- Gateway/device integrators submitting equipment telemetry.
- Platform operators monitoring ingestion health and event status.
- Service developers consuming telemetry for downstream domain workflows.

## Outcome hypothesis

- Expected user outcome: a gateway can submit one telemetry event and an
  operator can observe whether it was accepted, processed, or dead-lettered.
- Opportunity evidence: assumption based on the need for a reliable event
  backbone before downstream transit-operations features.
- Signal: local smoke test shows accepted -> published -> processed status;
  no baseline exists yet.
- Follow-up owner/date: product owner reviews after the first deployed release.

## Scope (in)

- .NET 8 LTS solution scaffold with an ingestion API, event worker, shared
  contracts, and test projects.
- CloudEvents-compatible JSON contract for equipment telemetry.
- API-key authentication for gateway/device callers.
- PostgreSQL persistence for accepted events, processed records, idempotency,
  and dead-letter records.
- Kafka-compatible broker topic `equipment.telemetry.v1`; local development
  uses Redpanda through Docker Compose.
- Worker consumption with bounded retries, poison-message dead-lettering,
  correlation IDs, structured logs, basic metrics, and health endpoints.
- OpenAPI document for the public ingestion/status API.
- Docker Compose local environment and a repeatable smoke script.

## Scope (out)

- Equipment registry, maintenance workflows, passenger identification,
  payments, loyalty, and browser UI.
- Production broker provisioning, multi-region deployment, and managed
  identity providers.
- mTLS, OAuth2/OIDC, and fine-grained device authorization beyond the MVP API
  key.
- Real hardware certification or vendor-specific gateway protocols.

## Acceptance criteria

<!-- Scenario(s): use scenario names verbatim from EXAMPLES.feature and/or
     @w<N> tags — ship_validate resolves both for traceability. -->

| # | Criterion | Scenario(s) |
|---|-----------|-------------|
| 1 | API accepts a valid CloudEvents-compatible telemetry event and returns 202 with an event id. | Gateway submits valid telemetry event |
| 2 | Missing required fields or invalid payload shape returns 400 ProblemDetails with a correlation id. | Telemetry payload with missing required fields is rejected |
| 3 | Requests without a valid API key are rejected before validation or persistence. | Telemetry request without valid API key is rejected |
| 4 | Accepted events are durably persisted and published to `equipment.telemetry.v1`. | Accepted telemetry event is persisted and published |
| 5 | Operators can query event status by id across accepted, processed, and dead-lettered states. | Telemetry event status can be queried |
| 6 | The worker records processed telemetry exactly once per event id. | Worker records processed telemetry exactly once |
| 7 | Duplicate broker delivery does not create duplicate processed records or side effects. | Duplicate telemetry delivery does not create duplicate processed record |
| 8 | An unprocessable event is moved to dead-letter state after bounded retries. | Poison telemetry event is dead-lettered after bounded retries |
| 9 | API and worker expose live/readiness health suitable for local orchestration. | Service health endpoints report readiness |

## Decisions

### Decision 1 — Runtime and service boundary

- Context & forces: MetroMesh needs a modern, maintainable backend with clear
  contracts, testability, and low operational overhead for a first event
  backbone. Throughput and latency matter, but correctness and operator
  visibility matter more for the first release.
- Options considered: a single ASP.NET Core modular monolith vs. separate
  API and worker deployables; Node.js/TypeScript backend vs. .NET 8 LTS.
- Decision & why: use .NET 8 LTS with two deployables — `telemetry-ingest` for
  synchronous API acceptance and `telemetry-worker` for asynchronous broker
  consumption. This keeps the public contract small while allowing the worker
  to scale independently. The simplest reversible alternative is a modular
  monolith, but separate deployables better match the event-driven target
  architecture without adding framework complexity.
- Consequences: positive — clear ownership, independent scaling, typed
  contracts, and straightforward health checks; negative — more projects,
  container orchestration, and local setup than a single process.
- Reversibility: two-way door for deployment topology; revisit if operational
  cost exceeds the benefit before the second feature.

### Decision 2 — Event contract and broker

- Context & forces: downstream services need a stable event envelope,
  ordering by subject where practical, replay for recovery, and protection
  against duplicate delivery. The contract must be language-neutral enough for
  future services while remaining easy to validate.
- Options considered: custom JSON envelope vs. CloudEvents-compatible JSON;
  Apache Kafka vs. Redpanda vs. Azure Event Hubs. Kafka API compatibility is
  required; the local broker can be lighter than a full Kafka cluster.
- Decision & why: use a CloudEvents-compatible JSON envelope on Kafka API
  topic `equipment.telemetry.v1`, with Redpanda for local Compose development.
  Redpanda is the simplest local broker that preserves the Kafka API contract;
  managed Kafka/Event Hubs can replace it later through configuration.
- Consequences: positive — standard envelope, replayable stream, local-first
  development, and broker portability; negative — Redpanda introduces a
  non-Apache license consideration and Kafka semantics still require careful
  idempotency handling.
- Reversibility: two-way door for broker implementation behind the Kafka API;
  revisit before production if licensing or managed-service requirements
  change.

### Decision 3 — Durability and idempotency

- Context & forces: gateways may retry, brokers may redeliver, and workers may
  crash after processing. The system must not lose accepted events or create
  duplicate processed records without an explicit policy.
- Options considered: broker-only processing vs. database-backed inbox/outbox
  and processed ledger; at-most-once vs. at-least-once delivery. At-least-once
  with idempotent consumers is the simplest correct baseline.
- Decision & why: use PostgreSQL tables for accepted events, processed events,
  and dead-letter records; publish through an outbox-style flow so API
  acceptance is durable before broker publication is acknowledged. Consumers
  write processed records keyed by event id, making retries safe.
- Consequences: positive — durable acceptance, queryable status, duplicate
  safety, and dead-letter evidence; negative — database becomes a required
  dependency for the API and adds migration/operational work.
- Reversibility: mostly two-way door; changing the persistence model later is
  expensive, so revisit only if scale requires a different store.

### Decision 4 — Authentication and secrets

- Context & forces: telemetry endpoints must not be open to arbitrary callers,
  but the MVP does not need a full identity provider. Secrets must never be
  committed.
- Options considered: no auth vs. API key vs. OIDC/mTLS now. API key is the
  simplest secure-enough baseline for local and early deployments.
- Decision & why: require `X-MetroMesh-Api-Key` for ingestion and status APIs,
  backed by environment configuration. OIDC/mTLS remain future hardening
  gates for production.
- Consequences: positive — simple to implement, test, and rotate; negative —
  weaker than certificate or identity-provider auth and unsuitable as the
  final production control.
- Reversibility: two-way door; adding OIDC/mTLS later is additive if the
  auth boundary stays behind middleware.

### Decision 5 — Failure handling and observability

- Context & forces: poison messages, broker outages, and worker crashes must
  not silently lose data or create unbounded retry loops. Operators need
  enough telemetry to diagnose failures without exposing payload secrets.
- Options considered: infinite retry vs. bounded retries with dead-letter;
  logs only vs. logs + metrics + traces. Bounded retry with a dead-letter
  table is the simplest reliable baseline.
- Decision & why: implement bounded retries, mark dead-letter after the limit,
  persist correlation/causation IDs, emit structured logs and basic metrics,
  and expose live/readiness endpoints.
- Consequences: positive — predictable failure behavior and operator
  visibility; negative — dead-letter handling adds schema and operational
  process requirements.
- Reversibility: two-way door; dead-letter storage can later move to a
  dedicated topic or service without changing the public contract.
