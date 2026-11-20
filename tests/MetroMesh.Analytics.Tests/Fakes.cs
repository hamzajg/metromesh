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

    public IReadOnlyList<RidershipReading> Readings => _readings.Values.ToList();
    public IReadOnlyDictionary<string, FailureEntry> Failures => _failures;

    public sealed class FailureEntry
    {
        public int Attempts;
        public string? LastError;
        public DateTimeOffset? DeadLetteredAt;
    }
}
