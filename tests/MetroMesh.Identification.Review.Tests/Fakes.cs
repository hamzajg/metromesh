using MetroMesh.Identification.Review.Contracts;
using MetroMesh.Identification.Review.Persistence;

namespace MetroMesh.Identification.Review.Tests;

public sealed class FakeIdentificationStore : IIdentificationStore
{
    private readonly Dictionary<string, CustomerStationInteraction> _interactions = new(StringComparer.Ordinal);
    private readonly HashSet<string> _processed = new(StringComparer.Ordinal);

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

    public IReadOnlyList<CustomerStationInteraction> Interactions => _interactions.Values.ToList();
}
