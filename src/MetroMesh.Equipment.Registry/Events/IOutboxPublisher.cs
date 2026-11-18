namespace MetroMesh.Equipment.Registry.Events;

public interface IOutboxPublisher
{
    Task<int> PublishPendingAsync(CancellationToken cancellationToken = default);
}
