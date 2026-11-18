# ridership-analytics — specifications by example
# Scenario tags @w<N> map items to delivery waves (see WAVES.md / BACKLOG.md).

Feature: ridership-analytics

  @w1 @happy-path
  Scenario: Telemetry events are consumed and stored as ridership readings
    Given telemetry events on the equipment telemetry topic
    When the analytics consumer processes them
    Then ridership readings are persisted with station, equipment, timestamp, and count

  @w1
  Scenario: Redelivered events do not double-count readings
    Given an event already marked processed by the analytics consumer
    When the same event is delivered again
    Then the reading count is unchanged

  @w1
  Scenario: Unparseable or irrelevant events are handled without crashing
    Given a message that is not a valid telemetry event or carries no ridership data
    When the analytics consumer receives it
    Then the consumer skips or dead-letters it and continues processing

  @w1
  Scenario: Analytics service reports health
    Given the analytics service is running
    When a probe hits the live and ready endpoints
    Then both respond appropriately for orchestrators

  @w2
  Scenario: Counts are queryable by station and equipment over a time range
    Given ridership readings exist for a station and equipment
    When the caller queries counts with a time range filter
    Then aggregated counts for the window are returned

  @w2
  Scenario: Per-bucket time series are returned for trend inspection
    Given readings span multiple time buckets
    When the caller requests a bucketed series for a station
    Then one count per bucket is returned in chronological order

  @w2
  Scenario: Queries without a valid API key are rejected
    Given no API key or a wrong key
    When the caller hits any analytics query endpoint
    Then the request is rejected before validation

  @w2
  Scenario: Empty windows return zero counts, not errors
    Given no readings exist for the requested station and window
    When the caller queries counts for that window
    Then zero counts are returned with a 200 response

  @w3
  Scenario: Service runs in the compose stack alongside existing services
    Given the compose stack is running
    When the analytics service starts
    Then it consumes the telemetry topic and serves queries alongside ingest, worker, registry, and maintenance
