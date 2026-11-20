using Confluent.Kafka;
using System.Text.Json;
using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Passenger.Loyalty.Persistence;
using MetroMesh.Telemetry.Core.Contracts;

namespace MetroMesh.Passenger.Loyalty.Services;

public sealed class PassengerEventProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IPassengerStore _store;
    private readonly ILogger<PassengerEventProcessor> _logger;

    public PassengerEventProcessor(IPassengerStore store, ILogger<PassengerEventProcessor> logger)
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
            throw new InvalidOperationException("Malformed passenger envelope.", ex);
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Id))
        {
            throw new InvalidOperationException("Passenger envelope is missing an event id.");
        }

        var marked = await _store.TryMarkProcessedAsync(envelope.Id, cancellationToken);
        if (!marked)
        {
            _logger.LogInformation(
                "Passenger event {EventId} already processed; treating as duplicate",
                envelope.Id);
            return false;
        }

        var data = envelope.Data;
        var payload = data?.Payload;
        string profileReference = "unknown";
        if (payload is not null && payload.Value.TryGetProperty("profileReference", out var pref) && pref.ValueKind == JsonValueKind.String)
        {
            profileReference = pref.GetString() ?? "unknown";
        }

        var profile = new PassengerProfile(
            envelope.Id,
            profileReference,
            data?.EquipmentId ?? "unknown",
            "active",
            data?.OccurredAt ?? envelope.Time,
            data?.OccurredAt ?? envelope.Time);

        await _store.RecordPassengerAsync(profile, cancellationToken);

        _logger.LogInformation(
            "Recorded passenger profile for event {EventId} reference {ProfileReference}",
            envelope.Id,
            profile.ProfileReference);

        return true;
    }
}
