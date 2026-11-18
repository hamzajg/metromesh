# Spec: maintenance-operations

> Spec-driven development artifact. This file is the contract: implementation
> is done when every acceptance criterion maps to a scenario in
> `EXAMPLES.feature` and every scenario has a passing check.

## Problem

Stations and equipment are registered and telemetry flows through the
backbone, but there is no way to turn faults, threshold breaches, or operator
reports into tracked maintenance work. Technicians have no work-order
lifecycle, no assignment or completion history, and downstream consumers get
no signal when maintenance state changes.

## Users

- Maintenance planners raising requests from telemetry signals or operator
  reports.
- Technicians assigned to, starting, and completing work orders.
- Operations staff querying request/work-order status and history.

## Outcome hypothesis

- Expected user outcome: every maintenance need becomes a tracked request
  that flows into an assignable, completable work order with an auditable
  history.
- Opportunity evidence: product dossier identifies maintenance requests,
  schedules, work orders, and technician workflow as a core domain
  capability that consumes registry and telemetry context.
- Signal: work-order API coverage of request intake, lifecycle
  transitions, and history queries; no numeric target set.
- Follow-up owner/date: product owner, after first technician workflow
  integration.

## Scope (in)

- Maintenance request intake: reason, severity, source (manual/signal),
  equipment association.
- Work orders: create from request or directly, assign to technician,
  start, complete, cancel; type and priority fields.
- Work-order history: status transitions recorded and queryable.
- Equipment validation against the registry service before accepting
  work.
- Domain events (`maintenance.request.*`, `maintenance.workorder.*`)
  published via outbox to a Kafka-compatible topic.
- API-key auth, correlation IDs, ProblemDetails errors, health probes,
  Postgres persistence, Docker Compose wiring.

## Scope (out)

- Actual notification delivery (email/SMS/webhook dispatch) — deferred to
  the notifications feature; this feature emits domain events only.
- Maintenance schedules/recurrence engine.
- Threshold/fault detection automation — this feature accepts signal
  intake; detection logic stays in telemetry/worker concerns.
- Browser UI.

## Acceptance criteria

| # | Criterion | Scenario(s) |
|---|-----------|-------------|
| 1 | Planner can raise a maintenance request for registered equipment | @w1 |
| 2 | Planner can create and retrieve a work order linked to equipment | @w1 |
| 3 | Invalid payloads return ProblemDetails validation errors | @w1 |
| 4 | Requests without a valid API key are rejected | @w1 |
| 5 | Work order can be assigned, started, completed, or cancelled with history | @w2 |
| 6 | Work against unknown or decommissioned equipment is rejected | @w2 |
| 7 | Work-order and request status/history are queryable | @w2 |
| 8 | Maintenance mutations emit domain events to the event backbone | @w3 |
| 9 | Service exposes live/readiness health and runs in the compose stack | @w3 |

## Decisions

- **Separate maintenance bounded-context service.** Context & forces:
  work orders and requests are a distinct domain from equipment registry
  and telemetry ingestion; coupling them into either existing service
  would blur write ownership. Options: (a) new
  `MetroMesh.Maintenance` ASP.NET Core service sharing
  `MetroMesh.Telemetry.Core`; (b) extend the registry service (rejected —
  registry owns equipment lifecycle, not work tracking); (c) module inside
  the ingest API (rejected — ingest is a write-path API, not a domain
  service). Decision: (a) — new service, shared Core contracts/event bus,
  own Postgres schema and outbox topic `maintenance.workorder.v1`.
  Consequences: + clean bounded context, independent scaling; − another
  deployable. Reversibility: two-way; modules can be merged later if
  operations cost dominates.
- **Synchronous equipment validation via registry API.** Context &
  forces: a work order against nonexistent or decommissioned equipment is
  meaningless, but maintenance must not own equipment state. Options:
  (a) call registry `GET /v1/equipment/{id}` at intake with a short
  timeout; (b) replicate equipment state via consumed registry events
  (eventually consistent, more moving parts); (c) no validation
  (rejected — allows orphan work). Decision: (a) — synchronous read-only
  validation, registry outage returns 503 ProblemDetails. Consequences:
  + simple, always-current check; − runtime dependency on registry
  availability for intake. Reversibility: two-way — can move to (b)
  later without API contract changes.
- **Domain events only; no notification delivery.** Context & forces:
  FR-05 requires notifying recipients, but a notification channel
  (email/SMS/webhook) is a separate deliverable. Options: (a) emit
  `maintenance.*` events and let a future notifications service consume
  them; (b) implement webhook/email dispatch now. Decision: (a) —
  outbox events on `maintenance.workorder.v1`; delivery deferred.
  Reversibility: two-way — adding a dispatcher later needs no contract
  change.
- **Postgres-per-service with outbox publishing.** Same durability model
  as the telemetry backbone and registry: maintenance tables + outbox
  rows in one Postgres database owned by the service; events published
  asynchronously. Alternatives: shared DB (rejected — violates
  bounded-context ownership), synchronous publish (rejected — loses
  durability). Reversibility: two-way.
