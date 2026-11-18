# Spec: station-equipment-registry

> Spec-driven development artifact. This file is the contract: implementation
> is done when every acceptance criterion maps to a scenario in
> `EXAMPLES.feature` and every scenario has a passing check.

## Problem

Telemetry events arrive with free-form `equipmentId`/`stationId` values but
there is no authoritative record of which stations and equipment exist, where
they are installed, or whether they are active. Operations cannot trust,
correlate, or act on telemetry without a registry that owns station and
equipment lifecycle state.

## Users

- Operations engineers registering and maintaining stations, turnstiles, and
  cameras.
- Downstream telemetry/maintenance consumers that need validated equipment
  context.

## Outcome hypothesis

- Expected user outcome: every telemetry event can be correlated to a
  registered station and equipment record with a known lifecycle status.
- Opportunity evidence: product dossier identifies equipment lifecycle and
  station management as core domain capabilities; assumption that registry
  precedes maintenance/analytics features.
- Signal: registry API coverage of station/equipment CRUD + lifecycle queries;
  no numeric target set.
- Follow-up owner/date: product owner, after first consumer integration.

## Scope (in)

- Station registration: name, address, status (open/closed/offline), location.
- Equipment registration: turnstiles and cameras with model, station
  association, install date, maintenance threshold, status.
- Install/decommission lifecycle, including station closure cascading to
  equipment decommission.
- Status/health queries by id and by station.
- Domain events (`station.*`, `equipment.*`) published via outbox to a
  Kafka-compatible topic.
- API-key auth, correlation IDs, ProblemDetails errors, health probes,
  Postgres persistence, Docker Compose wiring.

## Scope (out)

- Turn-count aggregation and analytics read models (later feature).
- Maintenance requests/work orders/schedules (later feature).
- Identification/passenger/loyalty domains.
- Browser UI.

## Acceptance criteria

| # | Criterion | Scenario(s) |
|---|-----------|-------------|
| 1 | Operator can register a station and retrieve it by id | @w1 |
| 2 | Operator can register turnstile and camera equipment against a station | @w1 |
| 3 | Invalid registration payloads return ProblemDetails validation errors | @w1 |
| 4 | Requests without a valid API key are rejected | @w1 |
| 5 | Equipment can be decommissioned and station closure cascades to equipment | @w2 |
| 6 | Equipment status and station inventory are queryable | @w2 |
| 7 | Registry mutations emit domain events to the event backbone | @w3 |
| 8 | Service exposes live/readiness health and runs in the compose stack | @w3 |

## Decisions

- **Separate registry bounded-context service.** Context & forces: stations
  and equipment are a distinct domain from telemetry ingestion; coupling them
  into the ingest API would blur write ownership. Options: (a) new
  `MetroMesh.Equipment.Registry` ASP.NET Core service sharing
  `MetroMesh.Telemetry.Core` persistence abstractions; (b) add endpoints to the
  ingest API (simplest, most reversible); (c) separate process from day one
  with duplicated infrastructure. Decision: (a) — new service, shared Core
  contracts/persistence patterns, own Postgres schema and outbox topic
  `equipment.registry.v1`. Consequences: + clean bounded context, independent
  scaling; − more deployables to operate. Reversibility: two-way; modules can
  be merged later if operations cost dominates.
- **Postgres-per-service with outbox publishing.** Same durability model as
  the telemetry backbone: registry tables + outbox rows in one Postgres
  database owned by the registry service; domain events published
  asynchronously. Alternatives: shared DB with ingest (rejected — violates
  bounded-context ownership), synchronous event publish (rejected — loses
  durability). Reversibility: two-way.
- **API-key auth for MVP.** Same `X-MetroMesh-Api-Key` + correlation ID
  middleware pattern as ingest; per-role authorization deferred. Revisit
  trigger: operator/admin role requirements or external consumers.
