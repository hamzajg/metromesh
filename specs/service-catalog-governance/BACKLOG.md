# Backlog: Service catalog and API governance

Items are USER STORIES in the parseable format below — keep the heading
shape `## US<n> — <story>` and the field lines intact so `wave_plan` can
parse them. Write the story as: "As a <role>, I want <goal>, so that
<value>" — the title after the dash IS the story.

Order stories by user value; dependencies express technical order only.

## US1 — As a developer, I want every MetroMesh HTTP service to publish an OpenAPI spec, so that I can discover and call its endpoints
- Scenarios: @w1
- Size: M
- Notes: SwaggerGen already present in all HTTP services; ensure /swagger/v1/swagger.json served in all environments, set OpenAPI info version.

## US2 — As a platform operator, I want each service to self-register with a service discovery agent, so that healthy instances are discoverable
- Scenarios: @w2
- Depends: US1
- Size: L
- Notes: Consul-compatible registration at startup; health checks backed by /health/live and /health/ready from feature 7.

## US3 — As a platform operator, I want a service catalog listing all deployables with versions and health, so that I can operate the platform
- Scenarios: @w3
- Depends: US2
- Size: M
- Notes: Catalog JSON listing ingest, registry, maintenance, analytics,
         identification-review, passenger-loyalty with health status.

## US4 — As a developer, I want to see service dependencies (e.g. maintenance -> registry), so that I understand the dependency graph
- Scenarios: @w4
- Depends: US3
- Size: S
- Notes: Declare maintenance->registry dependency; visible in catalog.
