# Decision review: Service catalog and API governance

## Decision context

The service catalog and API governance feature is cross-cutting across all
seven MetroMesh deployables (six HTTP services plus one worker). Key decisions:

1. **API versioning**: `/v1/*` path prefix already used by all routes; OpenAPI
   `info.version` set to `1.0.0` on all services. Semantic versioning reserved
   for future breaking changes.

2. **Service discovery**: Consul-compatible registration pattern (per ebook
   chapter 14). Each HTTP service self-registers at startup via the Consul
   HTTP API, with TCP/HTTP health checks backed by `/health/live` and
   `/health/ready` from feature 7. A local Consul container can be added to
   Compose for demonstration; production uses the operator's discovery
   platform.

3. **Service catalog**: Read-only JSON catalog endpoint (or documentation)
   listing all deployables, versions, endpoints, and health status. Not a
   full developer portal UI — that is out of scope for MVP.

4. **Dependency visibility**: Simple dependency declarations in code/config
   (e.g. maintenance depends on registry), exposed through the catalog.

## Alternatives considered

- Azure API Management: rejected for MVP — requires cloud provisioning and
  cost; OpenAPI + self-registration achieves discovery locally.
- Full developer portal UI: rejected — catalog JSON is sufficient for
  operators; UI can be added later.
- Sidecar-based registration (Consul connect): rejected — adds infrastructure
  complexity; direct API registration is simpler for MVP.

## Tradeoffs

- Self-registration couples services to the discovery agent; mitigated by
  keeping registration behind an interface (`IServiceCatalog`) so it can be
  swapped.
- Catalog health status is point-in-time; not a substitute for feature 7
  metrics and tracing.

## Decision

Use OpenAPI/Swagger on all HTTP services with `/v1/*` versioning, Consul-
compatible self-registration with health checks, and a read-only service
catalog JSON listing all deployables with versions, endpoints, and health.
