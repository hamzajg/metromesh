# Validation: Platform resilience and observability

Validation performed after all waves complete.

## Method

- `dotnet build` — solution builds with no warnings as errors.
- `dotnet test` — all test projects pass (stdlib-only where CI runs
  `--no-deps`; integration tests need Docker).
- `docker compose up --build` — local smoke path starts all 7 services plus
  Postgres and Redpanda.
- `curl http://localhost:<port>/metrics` — each HTTP service returns
  OpenTelemetry counters.

## Evidence

- Build output: (to be recorded)
- Test output: (to be recorded)
- Smoke test: (to be recorded)

## Result

PENDING — validation not yet performed.
