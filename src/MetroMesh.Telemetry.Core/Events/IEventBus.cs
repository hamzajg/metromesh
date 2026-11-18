namespace MetroMesh.Telemetry.Core.Events;

public interface IEventBus
{
    Task PublishAsync(string topic, string key, string value, CancellationToken cancellationToken = default);
}
