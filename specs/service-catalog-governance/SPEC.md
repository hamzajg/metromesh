# Spec: Service catalog and API governance

> Spec-driven development artifact. This file is the contract: implementation
> is done when every acceptance criterion maps to a scenario in
> `EXAMPLES.feature` and every scenario has a passing check.

## Problem

MetroMesh services expose OpenAPI/Swagger only in the Analytics service, and
there is no service discovery, registration, or catalog mechanism. Operators
and developers cannot discover which services exist, what endpoints they
offer, whether they are healthy, or which services depend on which others.
This makes the platform hard to operate and extend as it grows.

## Users

- Platform operators maintaining the service inventory and dependency map.
- Developers integrating with MetroMesh APIs who need discoverable OpenAPI
  specifications.
- Service owners registering new deployables and advertising their health.

## Outcome hypothesis

- Expected user outcome: a developer can browse a service catalog, retrieve
  the OpenAPI spec for any deployable, and see which instances are healthy
  through service discovery.
- Opportunity evidence: assumption based on the service catalog and API
  governance requirements in the product domain dossier.
- Signal: OpenAPI JSON available for all 7 services; service discovery
  registers healthy instances; catalog lists all deployables with versions.
- Follow-up owner/date: product owner reviews after the first deployed
  release.

## Scope (in)

- OpenAPI/Swagger document served by all 7 HTTP deployables (ingest,
  registry, maintenance, analytics, identification-review, passenger-loyalty;
  worker is a background service without HTTP).
- API versioning (`v1` path prefix present on all routes; OpenAPI `info`
  version field set).
- Service discovery/registration pattern: each HTTP service self-registers
  with a Consul-compatible agent at startup, with health checks backed by
  `/health/live` and `/health/ready`.
- Service catalog listing all deployables, versions, endpoints, and health
  status (read-only catalog endpoint or documentation).
- Dependency visibility: maintenance->registry dependency declared and
  discoverable.

## Scope (out)

- Consul production cluster (local Consul container optional for demo).
- Azure API Management or other API gateway (documented as an option, not
  implemented).
- Developer portal UI (catalog data exposed as JSON; UI is out of scope).
- mTLS, OAuth2/OIDC, fine-grained authorization beyond API key.

## Acceptance criteria

| AC | Scenario (from EXAMPLES.feature) | Verified by |
|----|----------------------------------|-------------|
| AC-1 | Every HTTP service serves OpenAPI JSON at `/swagger/v1/swagger.json` | Integration test |
| AC-2 | All routes versioned under `/v1/*`; OpenAPI info version set | Code review + test |
| AC-3 | Service self-registers with discovery agent at startup | Integration test |
| AC-4 | Service catalog lists all deployables with health status | Unit/test |
| AC-5 | Maintenance->registry dependency declared and visible | Unit/test |

## Related

- Depends on feature 7 (platform resilience and observability) for health
  endpoints (`/health/live`, `/health/ready`) used by discovery checks.
