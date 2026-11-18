using System.Collections.Concurrent;
using MetroMesh.Telemetry.Ingest.Contracts;
using MetroMesh.Telemetry.Ingest.Events;
using MetroMesh.Telemetry.Ingest.Persistence;

namespace MetroMesh.Telemetry.Ingest.Tests;

public sealed class FakeTelemetryEventStore : ITelemetryEventStore
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task InsertAcceptedAsync(TelemetryEventEnvelope envelope, string envelopeJson, CancellationToken cancellationToken = default)
    {
        _entries.GetOrAdd(envelope.Id, _ => new Entry
        {
            EventId = envelope.Id,
            CorrelationId = envelope.CorrelationId,
            EnvelopeJson = envelopeJson,
            Status = "accepted",
            AcceptedAt = DateTimeOffset.UtcNow
        });
        return Task.CompletedTask;
    }

    public Task<TelemetryEventStatusResponse?> GetStatusAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (!_entries.TryGetValue(eventId, out var entry))
        {
            return Task.FromResult<TelemetryEventStatusResponse?>(null);
        }

        return Task.FromResult<TelemetryEventStatusResponse?>(entry.ToResponse());
    }

    public Task<IReadOnlyList<StoredTelemetryEvent>> GetUnpublishedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        var pending = _entries.Values
            .Where(e => e.Status == "accepted" && e.PublishedAt is null)
            .OrderBy(e => e.AcceptedAt)
            .Take(batchSize)
            .Select(e => new StoredTelemetryEvent(e.EventId, e.EnvelopeJson, e.Attempts, e.CorrelationId))
            .ToList();

        return Task.FromResult<IReadOnlyList<StoredTelemetryEvent>>(pending);
    }

    public Task MarkPublishedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(eventId, out var entry))
        {
            entry.Status = "published";
            entry.PublishedAt = DateTimeOffset.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task MarkPublishFailedAsync(string eventId, string error, CancellationToken cancellationToken = default)
    {
        if (_entries.TryGetValue(eventId, out var entry))
        {
            entry.Attempts++;
            entry.LastError = error;
        }

        return Task.CompletedTask;
    }

    private sealed class Entry
    {
        public required string EventId { get; init; }
        public required string CorrelationId { get; init; }
        public required string EnvelopeJson { get; init; }
        public required string Status { get; set; }
        public required DateTimeOffset AcceptedAt { get; init; }
        public DateTimeOffset? PublishedAt { get; set; }
        public DateTimeOffset? ProcessedAt { get; set; }
        public DateTimeOffset? DeadLetteredAt { get; set; }
        public int Attempts { get; set; }
        public string? LastError { get; set; }

        public TelemetryEventStatusResponse ToResponse() => new(
            EventId,
            Status,
            AcceptedAt,
            PublishedAt,
            ProcessedAt,
            DeadLetteredAt,
            Attempts,
            LastError);
    }
}

public sealed class FakeEventBus : IEventBus
{
    private readonly List<(string Topic, string Key, string Value)> _published = [];

    public IReadOnlyList<(string Topic, string Key, string Value)> Published => _published;

    public Task PublishAsync(string topic, string key, string value, CancellationToken cancellationToken = default)
    {
        _published.Add((topic, key, value));
        return Task.CompletedTask;
    }
}
