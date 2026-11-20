Feature: Service catalog and API governance

  Background:
    Given all MetroMesh HTTP services are running in Docker Compose

  @w1
  Scenario: Every HTTP service serves OpenAPI JSON
    When I GET http://localhost:8080/swagger/v1/swagger.json
    Then the response status is 200
    And the body contains "openapi"

    When I GET http://localhost:8082/swagger/v1/swagger.json
    Then the response status is 200

    When I GET http://localhost:8083/swagger/v1/swagger.json
    Then the response status is 200

    When I GET http://localhost:8084/swagger/v1/swagger.json
    Then the response status is 200

    When I GET http://localhost:8085/swagger/v1/swagger.json
    Then the response status is 200

    When I GET http://localhost:8086/swagger/v1/swagger.json
    Then the response status is 200

  @w1
  Scenario: All routes are versioned under /v1/*
    When I inspect the OpenAPI spec for the ingest service
    Then all paths begin with "/v1/"

  @w1
  Scenario: Service self-registers with discovery agent at startup
    When a MetroMesh HTTP service starts
    Then it registers itself with the Consul agent at http://consul:8500
    And it registers health checks for /health/live and /health/ready

  @w1
  Scenario: Service catalog lists all deployables with health status
    When I query the service catalog
    Then it lists ingest, registry, maintenance, analytics,
         identification-review, and passenger-loyalty
    And each entry includes version, endpoint, and health status

  @w1
  Scenario: Maintenance->registry dependency is declared and visible
    When I inspect the dependency catalog
    Then maintenance lists registry as a dependency
