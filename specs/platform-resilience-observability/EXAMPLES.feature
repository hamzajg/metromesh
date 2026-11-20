Feature: Platform resilience and observability

  Background:
    Given all MetroMesh services are running in Docker Compose

  Scenario: Service exposes Prometheus metrics endpoint
    When I GET http://localhost:8080/metrics
    Then the response status is 200
    And the body contains "metromesh_requests_received_total"
    And the body contains "metromesh_events_published_total"

  Scenario: Correlation ID propagates from HTTP request into Kafka event envelope
    When I POST to http://localhost:8080/v1/events with header X-Correlation-Id: corr-test-1
    And the event is published to topic equipment.telemetry.v1
    Then the Kafka message envelope contains correlationId "corr-test-1"

  Scenario: Causation ID chains producer through Kafka to consumer
    When an event is produced with causation ID cause-1
    And a consumer processes the event
    Then the consumer log contains operation ID matching the correlation ID
    And the consumer log contains parent ID matching cause-1

  Scenario: Circuit breaker trips when downstream dependency is unavailable
    Given the equipment registry service is stopped
    When the maintenance service calls the registry directory
    Then the maintenance service returns a fallback error within the timeout
    And the circuit breaker state is "open"

  Scenario: Bounded retries before dead-lettering
    Given a poison event that always fails processing
    When the worker attempts to process it
    Then the event is retried up to MaxAttempts times
    And the event is dead-lettered after the final attempt

  Scenario: All services share resilience abstractions from Telemetry.Core
    When the solution builds
    Then MetroMesh.Telemetry.Core contains ICircuitBreakerPolicy
    And MetroMesh.Telemetry.Core contains IResilientHttpClient
