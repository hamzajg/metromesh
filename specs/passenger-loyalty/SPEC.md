# Spec: passenger-loyalty

> Spec-driven development artifact. This file is the contract: implementation
> is done when every acceptance criterion maps to a scenario in
> `EXAMPLES.feature` and every scenario has a passing check.

## Problem

The platform serves transit passengers but has no bounded context that owns
passenger profiles, loyalty program membership, or change notifications.
Passenger identity and loyalty state cannot be tracked, queried, or acted on,
and operators cannot notify passengers when their profile or loyalty status
changes.

## Users

- Passengers managing their profile and loyalty membership.
- Operations staff configuring passenger records and loyalty programs.
- Downstream services (fare/payment, analytics, notifications) that need
  validated passenger and loyalty context.

## Outcome hypothesis

- Expected user outcome: every passenger has a durable profile and loyalty
  record, loyalty changes are tracked over time, and profile/loyalty updates
  emit notifications and domain events for downstream consumers.
- Opportunity evidence: product dossier identifies passenger/loyalty as a core
  domain capability (passenger profiles, loyalty programs, notifications);
  assumption that passenger/loyalty depends on telemetry backbone and station
  registry for event and station context.
- Signal: passenger profile + loyalty program CRUD, change notifications,
  domain event emission; no numeric target set.
- Follow-up owner/date: product owner, after first fare/payment integration.

## Scope (in)

- Passenger profile CRUD: id, profile reference, loyalty program reference.
- Passenger info updates: contact/profile attributes with change history.
- Loyalty program CRUD: id, passenger id, tier/status, terms.
- Loyalty program requests, updates, and retirement.
- Notification on profile/loyalty changes to configured recipients.
- API-key auth, correlation IDs, ProblemDetails validation errors, health
  probes, Postgres persistence, Docker Compose wiring.
- Domain events (`passenger.*`, `loyalty.*`) published via outbox to the
  event backbone.

## Scope (out)

- Fare/payment processing (later feature — deferred per dossier).
- Browser UI (operations console deferred — API-only backend first).
- Identification/passenger matching from cameras (identification-review
  feature).
- Ridership/analytics read models (ridership-analytics feature).

## Acceptance criteria

| # | Criterion | Scenario(s) |
|---|-----------|-------------|
| 1 | Operator can create a passenger profile and retrieve it by id | @w1 |
| 2 | Passenger profile info can be updated with change history | @w1 |
| 3 | Invalid passenger/loyalty payloads return ProblemDetails validation errors | @w1 |
| 4 | Requests without a valid API key are rejected | @w1 |
| 5 | Operator can create, update, and retire a loyalty program | @w2 |
| 6 | Profile/loyalty changes trigger notifications to configured recipients | @w2 |
| 7 | Passenger/loyalty mutations emit domain events to the event backbone | @w3 |
| 8 | Service exposes live/readiness health and runs in the compose stack | @w3 |

## Decisions

- **Separate passenger-loyalty bounded-context service.** Context & forces:
  passenger/loyalty is a distinct domain with its own lifecycle and change
  notification policy; coupling into telemetry ingest or registry would blur
  ownership and mix passenger PII with equipment concerns. Options: (a) new
  `MetroMesh.Passenger.Loyalty` ASP.NET Core service sharing
  `MetroMesh.Telemetry.Core` persistence abstractions; (b) add endpoints to
  the registry API (simplest, most reversible but violates bounded context);
  (c) separate process from day one with duplicated infrastructure.
  Decision: (a) — new service, shared Core contracts/persistence patterns,
  own Postgres schema and outbox topic `passenger.loyalty.v1`.
  Consequences: + clean bounded context, independent scaling, isolated
  passenger PII; − more deployables to operate. Reversibility: two-way;
  modules can be merged later if operations cost dominates.

- **Postgres-per-service with outbox publishing.** Same durability model as
  telemetry backbone, registry, analytics, and identification: passenger/
  loyalty tables + outbox rows in one Postgres database owned by the service;
  domain events published asynchronously. Alternatives: shared DB (rejected —
  violates bounded context), synchronous event publish (rejected — loses
  durability). Reversibility: two-way.

- **API-key auth for MVP.** Same `X-MetroMesh-Api-Key` + correlation ID
  middleware pattern as ingest; per-role authorization deferred. Revisit
  trigger: passenger/operator role requirements or external consumers.

- **Change notification at write.** Profile/loyalty changes generate
  notification records at write time and domain events for downstream
  consumers. Alternatives: poll-based change feed (rejected — higher latency);
  synchronous notification send (rejected — loses durability). Reversibility:
  two-way; notification channel is configurable.
