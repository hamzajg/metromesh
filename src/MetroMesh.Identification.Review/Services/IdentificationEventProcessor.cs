using System.Text.Json;
using MetroMesh.Identification.Review.Contracts;
using MetroMesh.Identification.Review.Persistence;
using MetroMesh.Telemetry.Core.Contracts;

namespace MetroMesh.Identification.Review.Services;

public sealed class IdentificationEventProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IIdentificationStore _store;
    private readonly ILogger<IdentificationEventProcessor> _logger;

    public IdentificationEventProcessor(IIdentificationStore store, ILogger<IdentificationEventProcessor> logger)
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
            throw new InvalidOperationException("Malformed identification envelope.", ex);
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Id))
        {
            throw new InvalidOperationException("Identification envelope is missing an event id.");
        }

        var marked = await _store.TryMarkProcessedAsync(envelope.Id, cancellationToken);
        if (!marked)
        {
            _logger.LogInformation(
                "Identification event {EventId} already processed; treating as duplicate",
                envelope.Id);
            return false;
        }

        var data = envelope.Data;
        var payload = data?.Payload;
        string matchResult = "none";
        if (payload is not null && payload.Value.TryGetProperty("matchResult", out var mr) && mr.ValueKind == JsonValueKind.String)
        {
            matchResult = mr.GetString() ?? "none";
        }

        var interaction = new CustomerStationInteraction(
            envelope.Id,
            data?.StationId ?? "unknown",
            data?.EquipmentId ?? "unknown",
            data?.EventType ?? envelope.Type,
            data?.OccurredAt ?? envelope.Time,
            matchResult,
            TryGetConfidence(payload),
            TryGetString(payload, "passengerId"),
            TryGetString(payload, "offenderId"),
            HasMatch(payload) ? matchResult : null);

        await _store.RecordInteractionAsync(interaction, cancellationToken);

        _logger.LogInformation(
            "Recorded customer station interaction for event {EventId} station {StationId} match {MatchResult}",
            envelope.Id,
            interaction.StationId,
            interaction.MatchResult);

        return true;
    }

    private static double? TryGetConfidence(JsonElement? payload)
    {
        if (payload is null || !payload.Value.TryGetProperty("confidence", out var conf))
        {
            return null;
        }

        if (conf.ValueKind == JsonValueKind.Number && conf.TryGetDouble(out var value))
        {
            return value;
        }

        return null;
    }

    private static string? TryGetString(JsonElement? payload, string propertyName)
    {
        if (payload is null || !payload.Value.TryGetProperty(propertyName, out var prop))
        {
            return null;
        }

        return prop.ValueKind == JsonValueKind.String ? prop.GetString() : null;
    }

    private static bool HasMatch(JsonElement? payload)
    {
        if (payload is null || !payload.Value.TryGetProperty("matchResult", out var result))
        {
            return false;
        }

        var match = result.GetString() ?? string.Empty;
        return !string.Equals(match, "none", StringComparison.OrdinalIgnoreCase);
    }
}
