namespace MetroMesh.Maintenance.Events;

public interface IOutboxPublisher
{
    Task<int> PublishPendingAsync(CancellationToken cancellationToken = default);
}
