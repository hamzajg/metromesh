# Waves — service-catalog-governance

Each wave IS an iteration: a time-boxed, independently shippable
increment — one branch, one PR, green tests, demoable outcome.
Do not start wave N+1 until wave N merges.

## Wave 1 — iteration 1

Scenarios proven by this wave:
- Every HTTP service serves OpenAPI JSON

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US1 | As a developer, I want every MetroMesh HTTP service to publish an OpenAPI spec, so that I can discover and call its endpoints | M | — | SwaggerGen present; serve /swagger/v1/swagger.json in all environments; set OpenAPI info version. |

## Wave 2 — iteration 2

Scenarios proven by this wave:
- Service self-registers with discovery agent at startup

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US2 | As a platform operator, I want each service to self-register with a service discovery agent, so that healthy instances are discoverable | L | US1 | Consul-compatible registration at startup; health checks from feature 7. |

## Wave 3 — iteration 3

Scenarios proven by this wave:
- Service catalog lists all deployables with health status

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US3 | As a platform operator, I want a service catalog listing all deployables with versions and health, so that I can operate the platform | M | US2 | Catalog JSON listing all deployables with health status. |

## Wave 4 — iteration 4

Scenarios proven by this wave:
- Maintenance->registry dependency is declared and visible

| Item | Title | Size | Deps | Notes |
|------|-------|------|------|-------|
| US4 | As a developer, I want to see service dependencies, so that I understand the dependency graph | S | US3 | Declare maintenance->registry dependency; visible in catalog. |
