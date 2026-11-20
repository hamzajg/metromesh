using System.Text.Json;
using MetroMesh.Equipment.Registry.Persistence;

namespace MetroMesh.Equipment.Registry.Events;

public static class RegistryEvents
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task EmitAsync(
        IRegistryStore store,
        string eventType,
        string aggregateId,
        object data,
        string? correlationId = null,
        string? causationId = null,
        CancellationToken cancellationToken = default)
    {
        var envelope = new
        {
            id = $"evt_{Guid.NewGuid():N}",
            type = eventType,
            source = "metromesh-registry",
            subject = aggregateId,
            time = DateTimeOffset.UtcNow,
            datacontenttype = "application/json",
            correlationId,
            causationId,
            data
        };

        var envelopeJson = JsonSerializer.Serialize(envelope, JsonOptions);
        await store.InsertOutboxEventAsync(eventType, aggregateId, envelopeJson, cancellationToken);
    }
}
