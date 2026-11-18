namespace MetroMesh.Equipment.Registry.Contracts;

public sealed record StationRequest(
    string? Name,
    string? Address,
    string? Status,
    string? Location);

public sealed record StationResponse(
    string Id,
    string Name,
    string Address,
    string Status,
    string? Location,
    DateTimeOffset CreatedAt);

public sealed record EquipmentRequest(
    string? Kind,
    string? Model,
    string? StationId,
    DateOnly? InstallDate,
    int? MaintenanceThreshold,
    string? Status);

public sealed record EquipmentResponse(
    string Id,
    string Kind,
    string Model,
    string StationId,
    DateOnly? InstallDate,
    int? MaintenanceThreshold,
    string Status,
    DateTimeOffset CreatedAt);
