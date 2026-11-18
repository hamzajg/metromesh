# maintenance-operations — specifications by example
# Scenario tags @w<N> map items to delivery waves (see WAVES.md / BACKLOG.md).

Feature: maintenance-operations

  @w1 @happy-path
  Scenario: Planner raises a maintenance request
    Given a valid API key and registered equipment
    When the planner posts a request with reason, severity, and equipment id
    Then the request is persisted and returned with an id and open status

  @w1
  Scenario: Planner creates a work order for equipment
    Given a valid API key and registered equipment
    When the planner posts a work order with type, priority, and equipment id
    Then the work order is persisted and returned with an id and open status

  @w1
  Scenario: Invalid maintenance payload is rejected
    Given a valid API key
    When the planner posts a work order missing required fields
    Then a ProblemDetails validation error is returned with a correlation id

  @w1
  Scenario: Maintenance request without valid API key is rejected
    Given no API key or a wrong key
    When the caller hits any maintenance endpoint
    Then the request is rejected before validation

  @w2
  Scenario: Work order lifecycle transitions are recorded
    Given an open work order
    When a technician is assigned, the order is started, then completed
    Then each transition is persisted and the history is queryable

  @w2
  Scenario: Work against invalid equipment is rejected
    Given equipment that is unknown or decommissioned in the registry
    When the planner posts a request or work order for it
    Then the request is rejected with a ProblemDetails error

  @w2
  Scenario: Maintenance status and history are queryable
    Given requests and work orders in various states
    When the caller queries by id or lists by status/equipment
    Then current state and transition history are returned

  @w3
  Scenario: Maintenance mutation emits a domain event
    Given an open work order
    When the work order is assigned or completed
    Then a domain event is published to the maintenance topic and marked published

  @w3
  Scenario: Maintenance service reports health in compose stack
    Given the compose stack is running
    When a probe hits the maintenance health endpoints
    Then live and ready checks respond appropriately
