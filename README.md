# MetroMesh

MetroMesh transit operations platform — event-driven equipment telemetry backbone.

## What it does

- **Ingest API** (`src/MetroMesh.Telemetry.Ingest`) accepts CloudEvents-compatible
  JSON telemetry at `POST /v1/events`, authenticated with `X-MetroMesh-Api-Key`,
  with `X-Correlation-Id` propagation and RFC 7807-style validation errors.
- **Durable pipeline** persists accepted events in PostgreSQL and publishes them
  through a transactional outbox to the Kafka-compatible topic
  `equipment.telemetry.v1`.
- **Worker** (`src/MetroMesh.Telemetry.Worker`) consumes the topic and marks each
  event processed exactly once; duplicate deliveries are no-ops, and poison
  messages are retried a bounded number of times before dead-lettering.
- **Status + health** — `GET /v1/events/{eventId}` reports
  accepted/published/processed/dead-lettered state; `/health/live` and
  `/health/ready` support probes.
- **Registry API** (`src/MetroMesh.Equipment.Registry`, port 8082) manages
  stations and turnstile/camera equipment: registration with validation,
  lifecycle transitions, station-close cascade, inventory queries, and
  domain events published via outbox to `equipment.registry.v1`.
- **Maintenance API** (`src/MetroMesh.Maintenance`, port 8083) turns faults
  into tracked work: maintenance requests, work orders with an
  assign/start/complete/cancel lifecycle, transactional history, equipment
  validation against the registry, and domain events via outbox to
  `maintenance.workorder.v1`.
- **Analytics** (`src/MetroMesh.Analytics`, port 8084) consumes
  `equipment.telemetry.v1` in its own `analytics-worker` consumer group,
  writing ridership readings to PostgreSQL with idempotent processed-marking
  (redelivery cannot double-count); exposes `GET /v1/ridership/counts` and
  `GET /v1/ridership/series` with API-key auth, plus `/health/live` and
  `/health/ready`.

## Quickstart (local smoke)

```bash
./smoke.sh   # docker compose up --build, health checks, submit, status, then down
```

Or manually:

```bash
docker compose up --build
```

Then:

```bash
curl -s http://localhost:8080/health/ready
curl -s -X POST http://localhost:8080/v1/events \
  -H 'Content-Type: application/json' \
  -H 'X-MetroMesh-Api-Key: dev-api-key' \
  -H 'X-Correlation-Id: corr-1' \
  -d '{"specversion":"1.0","id":"evt_1","type":"equipment.telemetry.turnstile.count","source":"gateway-01","subject":"turnstile/t-1","time":"2026-11-18T00:00:00Z","datacontenttype":"application/json","data":{"equipmentId":"t-1","stationId":"s-1","eventType":"turnstile.count.incremented","occurredAt":"2026-11-18T00:00:00Z","payload":{"count":1}}}'
curl -s http://localhost:8080/v1/events/evt_1 -H 'X-MetroMesh-Api-Key: dev-api-key'
```

## Configuration

| Setting | Env var | Default |
|---|---|---|
| Postgres | `ConnectionStrings__TelemetryDb` or `METROMESH_DB` | required |
| Kafka bootstrap | `Kafka__BootstrapServers` | `localhost:9092` |
| Topic | `Kafka__TelemetryTopic` | `equipment.telemetry.v1` |
| Consumer group | `Kafka__ConsumerGroup` | `telemetry-worker` |
| API key | `MetroMesh__ApiKey` | required for `/v1/*` |
| Max processing attempts | `Worker__MaxAttempts` | `3` |
| Registry DB | `ConnectionStrings__RegistryDb` | required by registry service |
| Registry topic | `Kafka__RegistryTopic` | `equipment.registry.v1` |
| Registry outbox | `Outbox__Enabled` / `Outbox__PollIntervalMs` / `Outbox__BatchSize` | `true` / `1000` / `50` |
| Maintenance DB | `ConnectionStrings__MaintenanceDb` | required by maintenance service |
| Analytics DB | `ConnectionStrings__AnalyticsDb` or `METROMESH_DB` | required by analytics service |
| Maintenance topic | `Kafka__MaintenanceTopic` | `maintenance.workorder.v1` |
| Registry endpoint (for maintenance) | `Maintenance__Registry__BaseUrl` / `Maintenance__Registry__ApiKey` | `http://localhost:8082` / `MetroMesh__ApiKey` |

## Develop

```bash
dotnet restore
dotnet build
dotnet test
```

Stack: .NET 8, ASP.NET Core, `Microsoft.NET.Sdk.Worker`, Npgsql, Confluent.Kafka,
xUnit + Testcontainers (Postgres/Redpanda) for integration coverage.

## Specs

Feature specs and delivery-loop state live under `specs/`. See
`specs/equipment-telemetry-backbone/`, `specs/station-equipment-registry/`,
`specs/maintenance-operations/`, and `specs/ridership-analytics/` for the
shipped features.
