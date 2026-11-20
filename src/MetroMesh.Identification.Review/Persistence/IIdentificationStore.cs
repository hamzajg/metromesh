using MetroMesh.Identification.Review.Contracts;

namespace MetroMesh.Identification.Review.Persistence;

public interface IIdentificationStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default);
    Task RecordInteractionAsync(CustomerStationInteraction interaction, CancellationToken cancellationToken = default);
    Task<CustomerStationInteraction?> GetInteractionAsync(string interactionId, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
}
