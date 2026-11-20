# Gates: Service catalog and API governance

> Preconditions for phase transitions. Each `## G<n> — <title>` block is a
> gate. `ship_loop` refuses to advance past a phase while a blocking gate is
> pending. Definitions live here; status and evidence live in `LOOP.md`.

## G1 — Design decisions challenged
- Before: plan
- Type: artifact
- Owner: agent
- Resolver: agent
- Blocking: yes
- Check: file:specs/service-catalog-governance/DECISION-REVIEW.md
- Why: Architecture choices (Consul-compatible registration, catalog data
  shape, API versioning) must be reviewed before wave planning.

## G2 — Feature 7 health endpoints available
- Before: wave:1
- Type: technical
- Owner: agent
- Resolver: agent
- Blocking: yes
- Check: file:specs/platform-resilience-observability/DELIVERY-AUDIT.md
- Why: Service discovery health checks depend on /health/live and
  /health/ready from feature 7.
