namespace MetroMesh.Equipment.Registry.Persistence;

public interface IRegistryStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    Task<StationRecord> InsertStationAsync(string name, string address, string status, string? location, CancellationToken cancellationToken = default);

    Task<StationRecord?> GetStationAsync(string stationId, CancellationToken cancellationToken = default);

    Task<EquipmentRecord?> InsertEquipmentAsync(string kind, string model, string stationId, DateOnly? installDate, int? maintenanceThreshold, string status, CancellationToken cancellationToken = default);

    Task<EquipmentRecord?> GetEquipmentAsync(string equipmentId, CancellationToken cancellationToken = default);

    Task<StationRecord?> UpdateStationStatusAsync(string stationId, string status, CancellationToken cancellationToken = default);

    Task<EquipmentRecord?> UpdateEquipmentStatusAsync(string equipmentId, string status, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EquipmentRecord>> ListEquipmentByStationAsync(string stationId, CancellationToken cancellationToken = default);
}

public sealed record StationRecord(
    string Id,
    string Name,
    string Address,
    string Status,
    string? Location,
    DateTimeOffset CreatedAt);

public sealed record EquipmentRecord(
    string Id,
    string Kind,
    string Model,
    string StationId,
    DateOnly? InstallDate,
    int? MaintenanceThreshold,
    string Status,
    DateTimeOffset CreatedAt);
