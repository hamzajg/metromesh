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

public sealed record AssignWorkOrder(string Assignee);

public sealed record CompleteWorkOrder(string? Resolution);

public sealed record CancelWorkOrder(string? Reason);

public sealed record WorkOrderResponse(
    string Id,
    string? RequestId,
    string EquipmentId,
    string Type,
    string Priority,
    string Title,
    string? Description,
    string? Assignee,
    string? Resolution,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record WorkOrderHistoryEntry(
    string Id,
    string WorkOrderId,
    string FromStatus,
    string ToStatus,
    string? Note,
    DateTimeOffset CreatedAt);
