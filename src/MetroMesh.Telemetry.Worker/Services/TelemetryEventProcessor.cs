using System.Text.Json;
using MetroMesh.Telemetry.Core.Contracts;
using MetroMesh.Telemetry.Core.Persistence;

namespace MetroMesh.Telemetry.Worker.Services;

public sealed class TelemetryEventProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITelemetryEventStore _store;
    private readonly ILogger<TelemetryEventProcessor> _logger;

    public TelemetryEventProcessor(ITelemetryEventStore store, ILogger<TelemetryEventProcessor> logger)
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
            throw new InvalidOperationException("Malformed telemetry envelope.", ex);
        }

        if (envelope is null || string.IsNullOrWhiteSpace(envelope.Id))
        {
            throw new InvalidOperationException("Telemetry envelope is missing an event id.");
        }

        var marked = await _store.TryMarkProcessedAsync(envelope.Id, cancellationToken);
        if (marked)
        {
            _logger.LogInformation(
                "Processed telemetry event {EventId} with correlation {CorrelationId}",
                envelope.Id,
                envelope.CorrelationId);
        }
        else
        {
            _logger.LogInformation(
                "Telemetry event {EventId} was already processed or is unknown; treating as duplicate",
                envelope.Id);
        }

        return marked;
    }
}
