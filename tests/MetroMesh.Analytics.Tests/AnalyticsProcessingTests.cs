using System.Text.Json;
using MetroMesh.Analytics.Contracts;
using MetroMesh.Analytics.Persistence;
using MetroMesh.Analytics.Services;
using MetroMesh.Telemetry.Core.Contracts;
using Microsoft.Extensions.Logging.Abstractions;

namespace MetroMesh.Analytics.Tests;

public class AnalyticsProcessingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "Analytics records processed event exactly once")]
    public async Task Analytics_records_processed_event_exactly_once()
    {
        var store = new FakeAnalyticsStore();
        var processor = new AnalyticsEventProcessor(store, NullLogger<AnalyticsEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId, "st-1", "eq-1", new { count = 3 });

        var marked = await processor.ProcessAsync(json);

        Assert.True(marked);
        Assert.Equal(1, store.ProcessedCount);
        Assert.Single(store.Readings);
        var reading = store.Readings[0];
        Assert.Equal(eventId, reading.EventId);
        Assert.Equal("st-1", reading.StationId);
        Assert.Equal("eq-1", reading.EquipmentId);
        Assert.Equal(3, reading.PassengerCount);
    }

    [Fact(DisplayName = "Duplicate analytics delivery does not create duplicate reading")]
    public async Task Duplicate_analytics_delivery_does_not_create_duplicate_reading()
    {
        var store = new FakeAnalyticsStore();
        var processor = new AnalyticsEventProcessor(store, NullLogger<AnalyticsEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId, "st-1", "eq-1", new { count = 1 });

        var first = await processor.ProcessAsync(json);
        var second = await processor.ProcessAsync(json);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, store.ProcessedCount);
        Assert.Single(store.Readings);
    }

    [Fact(DisplayName = "Malformed envelope throws invalid operation")]
    public async Task Malformed_envelope_throws_invalid_operation()
    {
        var store = new FakeAnalyticsStore();
        var processor = new AnalyticsEventProcessor(store, NullLogger<AnalyticsEventProcessor>.Instance);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync("{ not json"));
        Assert.Contains("Malformed analytics envelope", ex.Message);
    }

    [Fact(DisplayName = "Envelope missing event id throws invalid operation")]
    public async Task Envelope_missing_event_id_throws_invalid_operation()
    {
        var store = new FakeAnalyticsStore();
        var processor = new AnalyticsEventProcessor(store, NullLogger<AnalyticsEventProcessor>.Instance);
        var json = JsonSerializer.Serialize(new { specversion = "1.0", type = "x" }, JsonOptions);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => processor.ProcessAsync(json));
        Assert.Contains("missing an event id", ex.Message);
    }

    [Fact(DisplayName = "Default passenger count is one when payload has no count")]
    public async Task Default_passenger_count_is_one_when_payload_has_no_count()
    {
        var store = new FakeAnalyticsStore();
        var processor = new AnalyticsEventProcessor(store, NullLogger<AnalyticsEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId, "st-2", "eq-2", null);

        await processor.ProcessAsync(json);

        Assert.Single(store.Readings);
        Assert.Equal(1, store.Readings[0].PassengerCount);
    }

    private static string EnvelopeJson(string eventId, string stationId, string equipmentId, object? payload)
    {
        var envelope = new TelemetryEventEnvelope(
            "1.0",
            eventId,
            "urn:metromesh:telemetry:equipment.v1",
            "urn:metromesh:device:gate-1",
            "equipment/gate-1",
            DateTimeOffset.UtcNow,
            "application/json",
            "corr-analytics-1",
            null,
            new TelemetryEventData(
                equipmentId,
                stationId,
                "turnstile.count.incremented",
                DateTimeOffset.UtcNow,
                payload is null ? null : JsonSerializer.SerializeToElement(payload)));

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }
}
