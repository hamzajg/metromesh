using MetroMesh.Telemetry.Ingest.Contracts;

namespace MetroMesh.Telemetry.Ingest.Persistence;

public interface ITelemetryEventStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task InsertAcceptedAsync(TelemetryEventEnvelope envelope, string envelopeJson, CancellationToken cancellationToken = default);

    Task<TelemetryEventStatusResponse?> GetStatusAsync(string eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredTelemetryEvent>> GetUnpublishedAsync(int batchSize, CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(string eventId, CancellationToken cancellationToken = default);

    Task MarkPublishFailedAsync(string eventId, string error, CancellationToken cancellationToken = default);
}

public sealed record StoredTelemetryEvent(
    string EventId,
    string EnvelopeJson,
    int Attempts,
    string? CorrelationId);
