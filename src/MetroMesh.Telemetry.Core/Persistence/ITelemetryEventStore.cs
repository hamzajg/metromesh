using MetroMesh.Telemetry.Core.Contracts;

namespace MetroMesh.Telemetry.Core.Persistence;

public interface ITelemetryEventStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task InsertAcceptedAsync(TelemetryEventEnvelope envelope, string envelopeJson, CancellationToken cancellationToken = default);

    Task<TelemetryEventStatusResponse?> GetStatusAsync(string eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredTelemetryEvent>> GetUnpublishedAsync(int batchSize, CancellationToken cancellationToken = default);

    Task MarkPublishedAsync(string eventId, CancellationToken cancellationToken = default);

    Task MarkPublishFailedAsync(string eventId, string error, CancellationToken cancellationToken = default);

    Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default);
}

public sealed record StoredTelemetryEvent(
    string EventId,
    string EnvelopeJson,
    int Attempts,
    string? CorrelationId);
