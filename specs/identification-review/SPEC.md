# Spec: identification-review

> Spec-driven development artifact. This file is the contract: implementation
> is done when every acceptance criterion maps to a scenario in
> `EXAMPLES.feature` and every scenario has a passing check.

## Problem

Camera and gateway devices produce identification results (passenger and
offender identifications) but there is no bounded context that owns them:
records of who passed through which station and when, alert notifications for
confirmed offender matches, privacy controls over biometric/PII data, and an
audit trail of human review decisions. Without this domain, identification
events are lost after ingestion and no review or retention policy can be
enforced.

## Users

- Operations analysts reviewing identification events and match alerts.
- Security personnel actioning confirmed offender matches with human review.
- Platform operators configuring privacy retention and audit policy.

## Outcome hypothesis

- Expected user outcome: every identification event produces a reviewable
  station interaction record, offender matches raise alerts, and PII is
  purged per retention policy with an auditable trail.
- Opportunity evidence: product dossier identifies identification and privacy
  as a core domain capability (FR-06); assumption that identification depends
  on telemetry backbone and station registry for event and station context.
- Signal: identification-event coverage mapped to interaction records,
  alert routing, retention purge, human-review gate; no numeric target set.
- Follow-up owner/date: product owner, after first camera integration.

## Scope (in)

- Ingest identification results from cameras/gateways (passenger and
  offender identifications) as CloudEvents-compatible envelopes.
- Record customer station interactions: passenger/offender id, station id,
  turnstile id, ingress/egress timestamp, match result, confidence.
- API-key auth, correlation IDs, ProblemDetails validation errors, health
  probes, Postgres persistence, Docker Compose wiring.
- Alert notifications on confirmed offender matches to configured recipients.
- Privacy controls: redact/purge non-match biometric/PII per retention policy.
- Human review gate before any adverse action on an identification.
- Audit trail of review decisions, purge events, and alert deliveries.
- Domain events (`identification.*`) published via outbox to the event
  backbone.

## Scope (out)

- Biometric matching / facial recognition computation (deferred — results
  are ingested, not produced; matching is optional/high-risk per dossier).
- Fare/payment and loyalty programs (later feature).
- Browser UI (operations console deferred — API-only backend first).
- Turn-count aggregation and analytics read models (ridership-analytics
  feature).

## Acceptance criteria

| # | Criterion | Scenario(s) |
|---|-----------|-------------|
| 1 | Identification results are ingested and stored as customer station interactions | @w1 |
| 2 | Invalid identification payloads return ProblemDetails validation errors | @w1 |
| 3 | Requests without a valid API key are rejected | @w1 |
| 4 | Confirmed offender matches trigger alert notifications | @w2 |
| 5 | Non-match biometric/PII is purged per retention policy | @w2 |
| 6 | Human review is required before adverse action on identifications | @w2 |
| 7 | Identification mutations emit domain events to the event backbone | @w3 |
| 8 | Service exposes live/readiness health and runs in the compose stack | @w3 |

## Decisions

- **Separate identification-review bounded-context service.** Context &
  forces: identification/privacy is a distinct domain with its own retention
  and review policy; coupling into telemetry ingest would blur ownership and
  mix high-risk PII handling with event backbone concerns. Options: (a) new
  `MetroMesh.Identification.Review` ASP.NET Core service sharing
  `MetroMesh.Telemetry.Core` persistence abstractions; (b) add endpoints to
  the ingest API (simplest, most reversible but violates bounded context);
  (c) separate process from day one with duplicated infrastructure.
  Decision: (a) — new service, shared Core contracts/persistence patterns,
  own Postgres schema and outbox topic `identification.review.v1`.
  Consequences: + clean bounded context, independent scaling, isolated PII
  retention; − more deployables to operate. Reversibility: two-way; modules
  can be merged later if operations cost dominates.

- **Postgres-per-service with outbox publishing.** Same durability model as
  telemetry backbone and registry: identification tables + outbox rows in one
  Postgres database owned by the service; domain events published
  asynchronously. Alternatives: shared DB (rejected — violates bounded
  context), synchronous event publish (rejected — loses durability).
  Reversibility: two-way.

- **API-key auth for MVP.** Same `X-MetroMesh-Api-Key` + correlation ID
  middleware pattern as ingest; per-role authorization deferred. Revisit
  trigger: operator/security role requirements or external consumers.

- **PII redaction at write, purge at retention.** Non-match biometric/PII
  fields are redacted on ingest (retain only what is needed for the
  interaction record) and purged by a retention job per configured window.
  Alternatives: retain everything and redact at read (rejected — increases
  exposure surface); purge on ingest (rejected — loses audit context).
  Reversibility: two-way; retention window is configurable.
