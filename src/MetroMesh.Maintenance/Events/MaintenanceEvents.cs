using System.Text.Json;
using MetroMesh.Maintenance.Persistence;

namespace MetroMesh.Maintenance.Events;

public static class MaintenanceEvents
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task EmitAsync(
        IMaintenanceStore store,
        string eventType,
        string aggregateId,
        object data,
        CancellationToken cancellationToken = default)
    {
        var envelope = new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = eventType,
            source = "metromesh-maintenance",
            subject = aggregateId,
            time = DateTimeOffset.UtcNow,
            datacontenttype = "application/json",
            data
        };

        var envelopeJson = JsonSerializer.Serialize(envelope, JsonOptions);
        await store.InsertOutboxEventAsync(eventType, aggregateId, envelopeJson, cancellationToken);
    }
}
