# AGENTS.md — MetroMesh

Guidance for AI agents (and humans) working in this repository.

## Project

MetroMesh is an event-driven equipment-telemetry backbone for transit
operations. Five deployables share `MetroMesh.Telemetry.Core`:

- `src/MetroMesh.Telemetry.Ingest` — ASP.NET Core API (`POST /v1/events`,
  `GET /v1/events/{id}`, `/health/live`, `/health/ready`).
- `src/MetroMesh.Telemetry.Worker` — Kafka consumer marking events processed
  exactly once with bounded retries and dead-lettering.
- `src/MetroMesh.Equipment.Registry` — ASP.NET Core station/equipment
  registry API (port 8082) with lifecycle transitions, inventory queries,
  and domain events via outbox to `equipment.registry.v1`.
- `src/MetroMesh.Maintenance` — ASP.NET Core maintenance API (port 8083):
  requests, work-order lifecycle with history, registry equipment
  validation, domain events via outbox to `maintenance.workorder.v1`.
- `src/MetroMesh.Analytics` — ASP.NET Core analytics API (port 8084):
  consumes `equipment.telemetry.v1`, writes idempotent ridership readings,
  exposes `GET /v1/ridership/counts` and `GET /v1/ridership/series` with
  API-key auth, plus health probes.
- `src/MetroMesh.Telemetry.Core` — shared contracts, `ITelemetryEventStore`
  (Postgres implementation), and the `IEventBus`/Kafka producer used by
  both outbox publishers. No web/worker dependencies.

## Commands

| Task | Command |
|---|---|
| Restore | `dotnet restore` |
| Build | `dotnet build` |
| Test | `dotnet test` (integration tests start Postgres/Redpanda containers — needs Docker) |
| Local smoke | `docker compose up --build` |

Target framework: .NET 8 (`net8.0`). Solution file is `MetroMesh.slnx`.

## Conventions

- Conventional commits (`feat:`, `fix:`, `chore:`, `docs:`); branch `feat/*`,
  `fix/*`, `chore/*`, `docs/*`; never commit directly to `main`.
- One logical change per PR; wave PRs stack on their predecessor.
- Keep the worker free of ASP.NET Core dependencies — shared code belongs in
  `MetroMesh.Telemetry.Core`.
- Secrets come from configuration/env only; never commit keys or `.env` files.
- Tests cite scenario names in `DisplayName` where they prove spec scenarios.

## Specs & delivery loop

- Specs live in `specs/<feature>/` (`SPEC.md`, `EXAMPLES.feature`, `BACKLOG.md`,
  `GATES.md`, `WAVES.md`, `LOOP.md`, `LOOP.state.json`, audits).
- Drive delivery with the OpenHarness oracle:
  `python -m openharness run ship_loop --action=next --repo_path=sources/metromesh`
  from the harness root. `ship_loop` is the only writer of `LOOP.md` /
  `LOOP.state.json` — never edit them by hand.
- Record wave completion only with test/CI evidence; keep acceptance status
  separate from PR merge and deployment state.
