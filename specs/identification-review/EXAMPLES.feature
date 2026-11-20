# identification-review — specifications by example
# Scenario tags @w<N> map items to delivery waves (see WAVES.md / BACKLOG.md).

Feature: identification-review

  @w1 @happy-path
  Scenario: Identification result is ingested and stored as a station interaction
    Given a valid API key
    When the camera posts a passenger identification with station, turnstile, and match result
    Then a customer station interaction record is persisted with an id and accepted status

  @w1
  Scenario: Offender identification is ingested and stored as a station interaction
    Given a valid API key
    When the camera posts an offender identification with match confidence
    Then the interaction record is persisted flagged as offender with confidence level

  @w1
  Scenario: Invalid identification payload is rejected
    Given a valid API key
    When the operator posts an identification missing required fields
    Then a ProblemDetails validation error is returned with a correlation id

  @w1
  Scenario: Identification request without valid API key is rejected
    Given no API key or a wrong key
    When any identification endpoint is called
    Then the request is rejected before validation

  @w2
  Scenario: Confirmed offender match triggers an alert notification
    Given an offender identification with confirmed match
    When the record is persisted
    Then an alert notification is queued to configured recipients

  @w2
  Scenario: Non-match biometric PII is purged per retention policy
    Given an identification record older than the retention window with no match
    When the retention purge runs
    Then biometric/PII fields are redacted and a purge event is audited

  @w2
  Scenario: Human review is required before adverse action
    Given an identification pending review
    When an adverse action is attempted without review approval
    Then the action is rejected pending human review

  @w2
  Scenario: Review decisions and purge events are audited
    Given a reviewed identification record
    When a reviewer approves or rejects the record
    Then an audit trail entry is persisted with reviewer, decision, and timestamp

  @w3
  Scenario: Identification mutation emits a domain event
    Given a stored station interaction record
    When the record is created or reviewed
    Then a domain event is published to the identification topic and marked published

  @w3
  Scenario: Identification service reports health in compose stack
    Given the compose stack is running
    When a probe hits the identification health endpoints
    Then live and ready checks respond appropriately
