using System.Text.Json;
using MetroMesh.Analytics.Contracts;
using MetroMesh.Analytics.Persistence;
using MetroMesh.Telemetry.Core.Contracts;

namespace MetroMesh.Analytics.Services;

public sealed class AnalyticsEventProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IAnalyticsStore _store;
    private readonly ILogger<AnalyticsEventProcessor> _logger;

    public AnalyticsEventProcessor(IAnalyticsStore store, ILogger<AnalyticsEventProcessor> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<bool> ProcessAsync(string envelopeJson, CancellationToken cancellationToken = default)
    {
        TelemetryEventEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<TelemetryEventEnvelope>(envelopeJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("Malformed analytics envelope.", ex);
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Id))
        {
            throw new InvalidOperationException("Analytics envelope is missing an event id.");
        }

        var marked = await _store.TryMarkProcessedAsync(envelope.Id, cancellationToken);
        if (!marked)
        {
            _logger.LogInformation(
                "Analytics event {EventId} already processed; treating as duplicate",
                envelope.Id);
            return false;
        }

        var data = envelope.Data;
        var reading = new RidershipReading(
            envelope.Id,
            data?.StationId ?? "unknown",
            data?.EquipmentId ?? "unknown",
            data?.EventType ?? envelope.Type,
            data?.OccurredAt ?? envelope.Time,
            ExtractPassengerCount(data?.Payload));

        await _store.RecordReadingAsync(reading, cancellationToken);

        _logger.LogInformation(
            "Recorded ridership reading for event {EventId} station {StationId} equipment {EquipmentId}",
            envelope.Id,
            reading.StationId,
            reading.EquipmentId);

        return true;
    }

    private static int ExtractPassengerCount(JsonElement? payload)
    {
        if (payload is null)
        {
            return 1;
        }

        if (payload.Value.TryGetProperty("count", out var count) && count.TryGetInt32(out var passengerCount))
        {
            return passengerCount;
        }

        return 1;
    }
}
