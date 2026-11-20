# passenger-loyalty — specifications by example
# Scenario tags @w<N> map items to delivery waves (see WAVES.md / BACKLOG.md).

Feature: passenger-loyalty

  @w1 @happy-path
  Scenario: Operator creates a passenger profile
    Given a valid API key
    When the operator posts a passenger with profile and loyalty references
    Then the passenger profile is persisted and returned with an id and active status

  @w1
  Scenario: Operator updates passenger profile info
    Given a registered passenger
    When the operator posts updated contact/profile attributes
    Then the passenger info is persisted with a change record and previous values retained

  @w1
  Scenario: Invalid passenger payload is rejected
    Given a valid API key
    When the operator posts a passenger missing required fields
    Then a ProblemDetails validation error is returned with a correlation id

  @w1
  Scenario: Passenger request without valid API key is rejected
    Given no API key or a wrong key
    When any passenger endpoint is called
    Then the request is rejected before validation

  @w2
  Scenario: Operator creates a loyalty program for a passenger
    Given a registered passenger
    When the operator posts a loyalty program with tier, status, and terms
    Then the loyalty program is persisted linked to that passenger

  @w2
  Scenario: Operator updates and retires a loyalty program
    Given a registered loyalty program
    When the operator updates its tier/status or retires it
    Then the loyalty program reflects the change and retirement is recorded

  @w2
  Scenario: Profile/loyalty change triggers a notification
    Given a registered passenger with a configured notification recipient
    When the passenger profile or loyalty program changes
    Then a notification is queued to the configured recipient

  @w2
  Scenario: Change notifications are audited
    Given a notification was queued for a passenger/loyalty change
    When the notification is delivered or fails
    Then an audit trail entry is persisted with recipient, status, and timestamp

  @w3
  Scenario: Passenger/loyalty mutation emits a domain event
    Given a stored passenger or loyalty record
    When the record is created or updated
    Then a domain event is published to the passenger-loyalty topic and marked published

  @w3
  Scenario: Passenger-loyalty service reports health in compose stack
    Given the compose stack is running
    When a probe hits the passenger-loyalty health endpoints
    Then live and ready checks respond appropriately
