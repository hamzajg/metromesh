namespace MetroMesh.Maintenance.Persistence;

public interface IMaintenanceStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    Task<MaintenanceRequestRecord> InsertRequestAsync(string equipmentId, string reason, string severity, string source, string? details, CancellationToken cancellationToken = default);

    Task<MaintenanceRequestRecord?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default);

    Task<WorkOrderRecord> InsertWorkOrderAsync(string? requestId, string equipmentId, string type, string priority, string title, string? description, CancellationToken cancellationToken = default);

    Task<WorkOrderRecord?> GetWorkOrderAsync(string workOrderId, CancellationToken cancellationToken = default);
}

public sealed record MaintenanceRequestRecord(
    string Id,
    string EquipmentId,
    string Reason,
    string Severity,
    string Source,
    string? Details,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record WorkOrderRecord(
    string Id,
    string? RequestId,
    string EquipmentId,
    string Type,
    string Priority,
    string Title,
    string? Description,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
