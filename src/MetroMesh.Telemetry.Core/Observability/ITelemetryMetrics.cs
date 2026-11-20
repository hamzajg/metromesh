namespace MetroMesh.Telemetry.Core.Observability;

public interface ITelemetryMetrics
{
    void IncrementRequestsReceived(string service, string endpoint);
    void IncrementEventsPublished(string topic);
    void IncrementEventsProcessed(string consumerGroup);
    void IncrementEventsDeadLettered(string topic, string reason);
    void IncrementCircuitBreakerTrips(string downstreamService);
    void RecordRequestDuration(string service, string endpoint, double milliseconds);
    string ToPrometheusFormat();
}
