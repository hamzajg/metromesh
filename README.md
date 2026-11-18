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
`specs/equipment-telemetry-backbone/` for the shipped backbone feature.
