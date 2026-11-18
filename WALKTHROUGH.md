# MetroMesh — Progress Walkthrough

Where the product stands today, what works, and how to see it.

## Big picture

MetroMesh is an event-driven transit-operations platform built spec-first:
each feature goes through discover → spec → audit → wave-by-wave PRs →
validation → delivery audit → merge. Specs and loop state live in `specs/`.

## Feature 1 — `equipment-telemetry-backbone` (SHIPPED, merged)

The event backbone every later feature builds on.

- **Ingest API** (`src/MetroMesh.Telemetry.Ingest`): `POST /v1/events`
  accepts CloudEvents-compatible JSON, requires `X-MetroMesh-Api-Key`,
  propagates `X-Correlation-Id`, returns ProblemDetails on invalid payloads.
- **Durable pipeline**: accepted events persist in PostgreSQL and publish
  through an outbox to Kafka topic `equipment.telemetry.v1`.
- **Worker** (`src/MetroMesh.Telemetry.Worker`): consumes the topic, marks
  each event processed exactly once, retries bounded times, then dead-letters
  poison messages with evidence.
- **Ops**: `/health/live` + `/health/ready` on API and worker; `compose.yaml`
  + `smoke.sh` run the whole stack locally.

**See it live:**

```bash
./smoke.sh
# → health checks, POST /v1/events (202), GET /v1/events/{id} status, worker health
```

Tests: `dotnet test` — 10/10 passing (incl. real Postgres + Redpanda
Testcontainers). Spec/audit trail: `specs/equipment-telemetry-backbone/`.

## Feature 2 — `station-equipment-registry` (SPECCED, ready to build)

Gives telemetry authoritative context: which stations and equipment exist,
where they are, and whether they are active.

- New bounded-context service `MetroMesh.Equipment.Registry` sharing
  `MetroMesh.Telemetry.Core`, own Postgres schema + outbox topic
  `equipment.registry.v1`.
- 3 waves: (1) station/equipment registration + auth + validation,
  (2) lifecycle + station-close cascade + inventory queries,
  (3) domain events + health + compose wiring.
- Spec PR merged; decision review ACCEPT; spec audit PASS.
  Details: `specs/station-equipment-registry/`.

## Requirements coverage (from product dossier)

| Area | Status |
|---|---|
| Event ingestion + backbone | Shipped |
| Equipment/station registry | Specced (next) |
| Maintenance ops, identification, analytics, notifications, passenger/loyalty | Not started |
| Metrics/tracing, service catalog, K8s/CI-CD | Not started |

## Try it yourself

```bash
dotnet restore && dotnet build && dotnet test
docker compose up --build   # or ./smoke.sh
```
