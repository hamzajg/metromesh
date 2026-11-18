namespace MetroMesh.Telemetry.Ingest.Events;

public interface IOutboxPublisher
{
    Task<int> PublishPendingAsync(CancellationToken cancellationToken = default);
}
