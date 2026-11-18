namespace MetroMesh.Maintenance.Services;

public interface IEquipmentDirectory
{
    Task<EquipmentDirectoryEntry?> GetEquipmentAsync(string equipmentId, CancellationToken cancellationToken = default);
}

public sealed record EquipmentDirectoryEntry(string Id, string Kind, string Status);
