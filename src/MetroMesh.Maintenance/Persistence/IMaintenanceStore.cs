namespace MetroMesh.Maintenance.Persistence;

public interface IMaintenanceStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<bool> PingAsync(CancellationToken cancellationToken = default);

    Task<MaintenanceRequestRecord> InsertRequestAsync(string equipmentId, string reason, string severity, string source, string? details, CancellationToken cancellationToken = default);

    Task<MaintenanceRequestRecord?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default);

    Task<WorkOrderRecord> InsertWorkOrderAsync(string? requestId, string equipmentId, string type, string priority, string title, string? description, CancellationToken cancellationToken = default);

    Task<WorkOrderRecord?> GetWorkOrderAsync(string workOrderId, CancellationToken cancellationToken = default);

    Task<WorkOrderRecord?> TransitionWorkOrderAsync(string workOrderId, string toStatus, string? assignee, string? resolution, string? note, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkOrderHistoryRecord>> ListWorkOrderHistoryAsync(string workOrderId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkOrderRecord>> ListWorkOrdersAsync(string? status, string? equipmentId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MaintenanceRequestRecord>> ListRequestsAsync(string? status, string? equipmentId, CancellationToken cancellationToken = default);

    Task UpdateRequestStatusAsync(string requestId, string status, CancellationToken cancellationToken = default);

    Task InsertOutboxEventAsync(string eventType, string aggregateId, string envelopeJson, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OutboxRecord>> GetUnpublishedOutboxAsync(int batchSize, CancellationToken cancellationToken = default);

    Task MarkOutboxPublishedAsync(string outboxId, CancellationToken cancellationToken = default);

    Task MarkOutboxFailedAsync(string outboxId, string error, CancellationToken cancellationToken = default);
}

public sealed record OutboxRecord(
    string Id,
    string EventType,
    string AggregateId,
    string EnvelopeJson,
    int Attempts);

public sealed record WorkOrderHistoryRecord(
    string Id,
    string WorkOrderId,
    string FromStatus,
    string ToStatus,
    string? Note,
    DateTimeOffset CreatedAt);

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
    string? Assignee,
    string? Resolution,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
