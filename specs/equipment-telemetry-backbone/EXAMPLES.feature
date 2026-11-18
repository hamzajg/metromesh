# Equipment telemetry backbone — specifications by example
# Scenario tags @w<N> map items to delivery waves (see WAVES.md / BACKLOG.md).

Feature: Equipment telemetry backbone

  @w1 @happy-path
  Scenario: Gateway submits valid telemetry event
    Given a configured gateway API key
    And a CloudEvents-compatible telemetry payload for a station turnstile
    When the gateway posts the event to the ingestion API
    Then the API returns 202 Accepted with an event id
    And the response includes a correlation id

  @w1
  Scenario: Telemetry payload with missing required fields is rejected
    Given a configured gateway API key
    And a telemetry payload missing the required subject field
    When the gateway posts the event to the ingestion API
    Then the API returns 400 with a ProblemDetails body
    And the response includes the same correlation id

  @w1
  Scenario: Telemetry request without valid API key is rejected
    Given no API key or an invalid API key
    When a caller posts a telemetry payload to the ingestion API
    Then the API returns 401 Unauthorized
    And no event is persisted or published

  @w2
  Scenario: Accepted telemetry event is persisted and published
    Given a valid telemetry event has been accepted by the API
    When the outbox publisher runs
    Then the event is published to equipment.telemetry.v1
    And the accepted event record is marked published

  @w2
  Scenario: Telemetry event status can be queried
    Given an accepted or processed telemetry event exists
    When an operator queries the event status API with the event id
    Then the API returns the current state, timestamps, and failure details when present

  @w3 @happy-path
  Scenario: Worker records processed telemetry exactly once
    Given a telemetry event is available on equipment.telemetry.v1
    When the worker consumes the event
    Then a processed record is stored with the event id and processing timestamp
    And structured logs include the correlation id

  @w3
  Scenario: Duplicate telemetry delivery does not create duplicate processed record
    Given the same telemetry event is delivered to the worker twice
    When both deliveries are processed
    Then only one processed record exists for the event id
    And downstream side effects are not duplicated

  @w4
  Scenario: Poison telemetry event is dead-lettered after bounded retries
    Given a telemetry event cannot be deserialized or validated by the worker
    When the worker attempts processing up to the configured retry limit
    Then the event is recorded in dead-letter state with the failure reason
    And the worker continues processing subsequent events

  @w4
  Scenario: Service health endpoints report readiness
    Given the API and worker are running with dependencies available
    When a probe requests the live and ready endpoints
    Then live returns success for process health
    And ready returns success only when required dependencies are reachable
