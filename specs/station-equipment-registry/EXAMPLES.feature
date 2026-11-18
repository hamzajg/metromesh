# station-equipment-registry — specifications by example
# Scenario tags @w<N> map items to delivery waves (see WAVES.md / BACKLOG.md).

Feature: station-equipment-registry

  @w1 @happy-path
  Scenario: Operator registers a station
    Given a valid API key
    When the operator posts a station with name, address, and status
    Then the station is persisted and returned with an id and accepted status

  @w1
  Scenario: Operator registers equipment against a station
    Given a registered station
    When the operator posts a turnstile or camera with model and station id
    Then the equipment is persisted linked to that station

  @w1
  Scenario: Invalid registry payload is rejected
    Given a valid API key
    When the operator posts equipment missing required fields
    Then a ProblemDetails validation error is returned with a correlation id

  @w1
  Scenario: Registry request without valid API key is rejected
    Given no API key or a wrong key
    When the operator calls any registry endpoint
    Then the request is rejected before validation

  @w2
  Scenario: Station closure cascades to equipment decommission
    Given a station with active equipment
    When the operator closes the station
    Then the station status becomes closed and its equipment is decommissioned

  @w2
  Scenario: Equipment status and station inventory are queryable
    Given registered stations and equipment
    When the operator queries equipment by id or lists a station's inventory
    Then current status and associations are returned

  @w3
  Scenario: Registry mutation emits a domain event
    Given a registered station
    When the operator decommissions equipment
    Then a domain event is published to the registry topic and marked published

  @w3
  Scenario: Registry service reports health in compose stack
    Given the compose stack is running
    When a probe hits the registry health endpoints
    Then live and ready checks respond appropriately
