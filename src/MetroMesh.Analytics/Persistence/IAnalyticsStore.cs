using MetroMesh.Analytics.Contracts;

namespace MetroMesh.Analytics.Persistence;

public interface IAnalyticsStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default);
    Task RecordReadingAsync(RidershipReading reading, CancellationToken cancellationToken = default);
    Task<bool> RecordProcessingFailureAsync(string eventId, string error, int maxAttempts, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RidershipCount>> GetCountsAsync(string? stationId, string? equipmentId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TimeBucketCount>> GetSeriesAsync(string? stationId, string? equipmentId, string bucket, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default);
}
