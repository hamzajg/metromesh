using System.Collections.Concurrent;
using System.Text.Json;
using MetroMesh.Analytics.Contracts;
using MetroMesh.Analytics.Persistence;
using MetroMesh.Telemetry.Core.Contracts;

namespace MetroMesh.Analytics.Tests;

public sealed class FakeAnalyticsStore : IAnalyticsStore
{
    private readonly ConcurrentDictionary<string, RidershipReading> _readings = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _processed = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, FailureEntry> _failures = new(StringComparer.Ordinal);

    public int ProcessedCount;
    public int ReadingCount;
    public bool PingOk { get; set; } = true;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (_processed.TryAdd(eventId, DateTimeOffset.UtcNow))
        {
            Interlocked.Increment(ref ProcessedCount);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task RecordReadingAsync(RidershipReading reading, CancellationToken cancellationToken = default)
    {
        _readings[reading.EventId] = reading;
        Interlocked.Increment(ref ReadingCount);
        return Task.CompletedTask;
    }

    public Task<bool> RecordProcessingFailureAsync(string eventId, string error, int maxAttempts, CancellationToken cancellationToken = default)
    {
        _failures.AddOrUpdate(
            eventId,
            _ => new FailureEntry { Attempts = 1, LastError = error },
            (_, entry) => { entry.Attempts++; entry.LastError = error; return entry; });

        var entry = _failures[eventId];
        var deadLettered = entry.Attempts >= maxAttempts;
        if (deadLettered)
        {
            entry.DeadLetteredAt = DateTimeOffset.UtcNow;
        }

        return Task.FromResult(deadLettered);
    }

    public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(PingOk);

    public Task<IReadOnlyList<RidershipCount>> GetCountsAsync(string? stationId, string? equipmentId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        var results = _readings.Values
            .Where(r => (stationId is null || r.StationId == stationId)
                 && (equipmentId is null || r.EquipmentId == equipmentId)
                 && (from is null || r.OccurredAt >= from)
                 && (to is null || r.OccurredAt <= to))
            .GroupBy(r => new { r.StationId, r.EquipmentId })
            .Select(g => new RidershipCount(g.Key.StationId, g.Key.EquipmentId, g.Sum(r => r.PassengerCount)))
            .OrderBy(r => r.StationId).ThenBy(r => r.EquipmentId)
            .ToList();

        return Task.FromResult<IReadOnlyList<RidershipCount>>(results);
    }

    public Task<IReadOnlyList<TimeBucketCount>> GetSeriesAsync(string? stationId, string? equipmentId, string bucket, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        var results = _readings.Values
            .Where(r => (stationId is null || r.StationId == stationId)
                 && (equipmentId is null || r.EquipmentId == equipmentId)
                 && (from is null || r.OccurredAt >= from)
                 && (to is null || r.OccurredAt <= to))
            .GroupBy(r => bucket switch
            {
                "day" => new DateTimeOffset(r.OccurredAt.Year, r.OccurredAt.Month, r.OccurredAt.Day, 0, 0, 0, TimeSpan.Zero),
                _ => new DateTimeOffset(r.OccurredAt.Year, r.OccurredAt.Month, r.OccurredAt.Day, r.OccurredAt.Hour, 0, 0, r.OccurredAt.Offset)
            })
            .Select(g => new TimeBucketCount(g.Key, g.Sum(r => r.PassengerCount)))
            .OrderBy(r => r.BucketStart)
            .ToList();

        return Task.FromResult<IReadOnlyList<TimeBucketCount>>(results);
    }

    public IReadOnlyList<RidershipReading> Readings => _readings.Values.ToList();
    public IReadOnlyDictionary<string, FailureEntry> Failures => _failures;

    public sealed class FailureEntry
    {
        public int Attempts;
        public string? LastError;
        public DateTimeOffset? DeadLetteredAt;
    }
}
