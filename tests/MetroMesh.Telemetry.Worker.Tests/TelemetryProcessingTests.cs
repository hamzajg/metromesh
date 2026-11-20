using System.Text.Json;
using MetroMesh.Telemetry.Core.Contracts;
using MetroMesh.Telemetry.Worker.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace MetroMesh.Telemetry.Worker.Tests;

public class TelemetryProcessingTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact(DisplayName = "Worker records processed telemetry exactly once")]
    public async Task Worker_records_processed_telemetry_exactly_once()
    {
        var store = new FakeTelemetryEventStore();
        var processor = new TelemetryEventProcessor(store, NullLogger<TelemetryEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId);

        var envelope = JsonSerializer.Deserialize<TelemetryEventEnvelope>(json, JsonOptions)!;
        await store.InsertAcceptedAsync(envelope, json);

        var marked = await processor.ProcessAsync(json);

        Assert.True(marked);
        Assert.Equal(1, store.ProcessedCount);

        var status = await store.GetStatusAsync(eventId);
        Assert.NotNull(status);
        Assert.Equal("processed", status.Status);
        Assert.NotNull(status.ProcessedAt);
    }

    [Fact(DisplayName = "Duplicate telemetry delivery does not create duplicate processed record")]
    public async Task Duplicate_telemetry_delivery_does_not_create_duplicate_processed_record()
    {
        var store = new FakeTelemetryEventStore();
        var processor = new TelemetryEventProcessor(store, NullLogger<TelemetryEventProcessor>.Instance);
        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId);

        var envelope = JsonSerializer.Deserialize<TelemetryEventEnvelope>(json, JsonOptions)!;
        await store.InsertAcceptedAsync(envelope, json);

        var first = await processor.ProcessAsync(json);
        var second = await processor.ProcessAsync(json);

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(1, store.ProcessedCount);

        var status = await store.GetStatusAsync(eventId);
        Assert.NotNull(status);
        Assert.Equal("processed", status.Status);
    }

    private static string EnvelopeJson(string eventId)
    {
        var envelope = new TelemetryEventEnvelope(
            "1.0",
            eventId,
            "urn:metromesh:telemetry:equipment.v1",
            "urn:metromesh:device:gate-1",
            "equipment/gate-1",
            DateTimeOffset.UtcNow,
            "application/json",
            "corr-worker-1",
            null,
            new TelemetryEventData(
                "eq-1",
                "st-1",
                "door.open",
                DateTimeOffset.UtcNow,
                JsonSerializer.SerializeToElement(new { value = 1 })));

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }
}
