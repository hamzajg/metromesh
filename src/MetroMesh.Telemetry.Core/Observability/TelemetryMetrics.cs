using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

namespace MetroMesh.Telemetry.Core.Observability;

public sealed class TelemetryMetrics : ITelemetryMetrics
{
    private static readonly Meter Meter = new("MetroMesh");
    private readonly Counter<int> _requestsReceived;
    private readonly Counter<int> _eventsPublished;
    private readonly Counter<int> _eventsProcessed;
    private readonly Counter<int> _eventsDeadLettered;
    private readonly Counter<int> _circuitBreakerTrips;
    private readonly Histogram<double> _requestDuration;

    private readonly ConcurrentDictionary<string, long> _counters = new();

    public TelemetryMetrics()
    {
        _requestsReceived = Meter.CreateCounter<int>("metromesh_requests_received_total");
        _eventsPublished = Meter.CreateCounter<int>("metromesh_events_published_total");
        _eventsProcessed = Meter.CreateCounter<int>("metromesh_events_processed_total");
        _eventsDeadLettered = Meter.CreateCounter<int>("metromesh_events_dead_lettered_total");
        _circuitBreakerTrips = Meter.CreateCounter<int>("metromesh_circuit_breaker_trips_total");
        _requestDuration = Meter.CreateHistogram<double>("metromesh_request_duration_seconds");
    }

    public void IncrementRequestsReceived(string service, string endpoint)
    {
        _requestsReceived.Add(1, new KeyValuePair<string, object?>("service", service), new KeyValuePair<string, object?>("endpoint", endpoint));
        IncrementCounter($"requests_received{{service=\"{service}\",endpoint=\"{endpoint}\"}}");
    }

    public void IncrementEventsPublished(string topic)
    {
        _eventsPublished.Add(1, new KeyValuePair<string, object?>("topic", topic));
        IncrementCounter($"events_published{{topic=\"{topic}\"}}");
    }

    public void IncrementEventsProcessed(string consumerGroup)
    {
        _eventsProcessed.Add(1, new KeyValuePair<string, object?>("consumer_group", consumerGroup));
        IncrementCounter($"events_processed{{consumer_group=\"{consumerGroup}\"}}");
    }

    public void IncrementEventsDeadLettered(string topic, string reason)
    {
        _eventsDeadLettered.Add(1, new KeyValuePair<string, object?>("topic", topic), new KeyValuePair<string, object?>("reason", reason));
        IncrementCounter($"events_dead_lettered{{topic=\"{topic}\",reason=\"{reason}\"}}");
    }

    public void IncrementCircuitBreakerTrips(string downstreamService)
    {
        _circuitBreakerTrips.Add(1, new KeyValuePair<string, object?>("downstream_service", downstreamService));
        IncrementCounter($"circuit_breaker_trips{{downstream_service=\"{downstreamService}\"}}");
    }

    public void RecordRequestDuration(string service, string endpoint, double milliseconds)
    {
        _requestDuration.Record(milliseconds / 1000.0, new KeyValuePair<string, object?>("service", service), new KeyValuePair<string, object?>("endpoint", endpoint));
    }

    public string ToPrometheusFormat()
    {
        var lines = new List<string>();
        foreach (var kv in _counters.OrderBy(k => k.Key))
        {
            lines.Add($"# HELP metromesh_{Sanitize(kv.Key)} MetroMesh counter");
            lines.Add($"# TYPE metromesh_{Sanitize(kv.Key)} counter");
            lines.Add($"metromesh_{Sanitize(kv.Key)} {kv.Value}");
        }
        return string.Join("\n", lines) + "\n";
    }

    private void IncrementCounter(string key)
    {
        _counters.AddOrUpdate(key, 1L, (_, v) => v + 1);
    }

    private static string Sanitize(string key) => string.Join("_", key.Split(new[] { '{', '}', ',', '=', '"' }, StringSplitOptions.RemoveEmptyEntries));
}
