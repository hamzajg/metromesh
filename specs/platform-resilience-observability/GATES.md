# Gates: Platform resilience and observability

> Preconditions for phase transitions. Each `## G<n> — <title>` block is a
> gate. `ship_loop` refuses to advance past a phase while a blocking gate is
> pending. Definitions live here; status and evidence live in `LOOP.md`.
>
> Gate phases: `spec` (before writing the spec), `plan` (before planning
> waves), `wave:<n>` (before starting wave n), `validate` (before validation),
> `finish` (before closing the feature).
>
> Gate types: decision | artifact | external | technical | quality | data.
> Resolver: `human` (needs a person to decide/provide something) or `agent`
> (the agent can satisfy it). Check: `manual`, `file:<repo-relative-path>`, or
> `command:<exact command>` (the agent runs it and records the output as
> evidence — the oracle never executes it).

## G1 — Design decisions challenged
- Before: plan
- Type: artifact
- Owner: agent
- Resolver: agent
- Blocking: yes
- Check: file:specs/platform-resilience-observability/DECISION-REVIEW.md
- Why: Every wave inherits the feature's architecture choices (OpenTelemetry
  exporters, circuit breaker implementation, shared core abstractions). The
  challenger review must exist before wave planning locks the design in.

## G2 — Local container runtime available
- Before: wave:1
- Type: technical
- Owner: agent
- Resolver: agent
- Blocking: yes
- Check: command:docker info --format '{{.ServerVersion}}'
- Why: The local Compose environment provides PostgreSQL and Redpanda used by
  integration checks and readiness probes.
