using MetroMesh.Identification.Review.Contracts;
using MetroMesh.Identification.Review.Persistence;

namespace MetroMesh.Identification.Review.Tests;

public sealed class FakeIdentificationStore : IIdentificationStore
{
    private readonly Dictionary<string, CustomerStationInteraction> _interactions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _processed = new(StringComparer.Ordinal);
    private readonly List<AlertNotification> _alerts = new();
    private readonly List<AuditEntry> _audit = new();
    private readonly Dictionary<string, ReviewDecision> _reviews = new(StringComparer.Ordinal);

    public int ProcessedCount;
    public bool PingOk { get; set; } = true;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (_processed.Add(eventId))
        {
            Interlocked.Increment(ref ProcessedCount);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task RecordInteractionAsync(CustomerStationInteraction interaction, CancellationToken cancellationToken = default)
    {
        _interactions[interaction.Id] = interaction;
        return Task.CompletedTask;
    }

    public Task<CustomerStationInteraction?> GetInteractionAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        _interactions.TryGetValue(interactionId, out var interaction);
        return Task.FromResult(interaction);
    }

    public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(PingOk);

    public Task RecordAlertAsync(AlertNotification alert, CancellationToken cancellationToken = default)
    {
        _alerts.Add(alert);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AlertNotification>> GetAlertsAsync(string? interactionId, CancellationToken cancellationToken = default)
    {
        var filtered = interactionId is not null
            ? _alerts.Where(a => a.InteractionId == interactionId).ToList()
            : _alerts.ToList();
        return Task.FromResult<IReadOnlyList<AlertNotification>>(filtered);
    }

    public Task RecordAuditAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        _audit.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> GetAuditAsync(string? interactionId, CancellationToken cancellationToken = default)
    {
        var filtered = interactionId is not null
            ? _audit.Where(a => a.InteractionId == interactionId).ToList()
            : _audit.ToList();
        return Task.FromResult<IReadOnlyList<AuditEntry>>(filtered);
    }

    public Task RecordReviewAsync(ReviewDecision decision, CancellationToken cancellationToken = default)
    {
        _reviews[decision.InteractionId] = decision;
        return Task.CompletedTask;
    }

    public Task<ReviewDecision?> GetReviewAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        _reviews.TryGetValue(interactionId, out var review);
        return Task.FromResult(review);
    }

    public Task<int> PurgeNonMatchOlderThanAsync(DateTimeOffset olderThan, string reason, CancellationToken cancellationToken = default)
    {
        var toPurge = _interactions
            .Where(kv => kv.Value.MatchResult == "none"
                      && kv.Value.OccurredAt < olderThan)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in toPurge)
        {
            _interactions.Remove(id);
        }

        return Task.FromResult(toPurge.Count);
    }

    public IReadOnlyList<CustomerStationInteraction> Interactions => _interactions.Values.ToList();
    public IReadOnlyList<AlertNotification> Alerts => _alerts;
    public IReadOnlyList<AuditEntry> AuditEntries => _audit;
    public IReadOnlyDictionary<string, ReviewDecision> Reviews => _reviews;
}
