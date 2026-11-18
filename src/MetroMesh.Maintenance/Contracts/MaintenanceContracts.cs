namespace MetroMesh.Maintenance.Contracts;

public sealed record CreateMaintenanceRequest(
    string EquipmentId,
    string Reason,
    string Severity,
    string? Source,
    string? Details);

public sealed record MaintenanceRequestResponse(
    string Id,
    string EquipmentId,
    string Reason,
    string Severity,
    string Source,
    string? Details,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record CreateWorkOrder(
    string EquipmentId,
    string Type,
    string Priority,
    string Title,
    string? Description,
    string? RequestId);

public sealed record WorkOrderResponse(
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
