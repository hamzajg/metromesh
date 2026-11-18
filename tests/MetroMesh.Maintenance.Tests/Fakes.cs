using System.Collections.Concurrent;
using MetroMesh.Maintenance.Persistence;
using MetroMesh.Maintenance.Services;

namespace MetroMesh.Maintenance.Tests;

public sealed class FakeMaintenanceStore : IMaintenanceStore
{
    private readonly ConcurrentDictionary<string, MaintenanceRequestRecord> _requests = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, WorkOrderRecord> _workOrders = new(StringComparer.Ordinal);

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);

    public Task<MaintenanceRequestRecord> InsertRequestAsync(string equipmentId, string reason, string severity, string source, string? details, CancellationToken cancellationToken = default)
    {
        var record = new MaintenanceRequestRecord(
            $"mr_{Guid.NewGuid():N}",
            equipmentId,
            reason,
            severity.ToLowerInvariant(),
            source.ToLowerInvariant(),
            details,
            "open",
            DateTimeOffset.UtcNow);
        _requests[record.Id] = record;
        return Task.FromResult(record);
    }

    public Task<MaintenanceRequestRecord?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default)
        => Task.FromResult(_requests.TryGetValue(requestId, out var record) ? record : null);

    public Task<WorkOrderRecord> InsertWorkOrderAsync(string? requestId, string equipmentId, string type, string priority, string title, string? description, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var record = new WorkOrderRecord(
            $"wo_{Guid.NewGuid():N}",
            requestId,
            equipmentId,
            type.ToLowerInvariant(),
            priority.ToLowerInvariant(),
            title,
            description,
            "open",
            now,
            now);
        _workOrders[record.Id] = record;
        return Task.FromResult(record);
    }

    public Task<WorkOrderRecord?> GetWorkOrderAsync(string workOrderId, CancellationToken cancellationToken = default)
        => Task.FromResult(_workOrders.TryGetValue(workOrderId, out var record) ? record : null);
}

public sealed class FakeEquipmentDirectory : IEquipmentDirectory
{
    private readonly ConcurrentDictionary<string, EquipmentDirectoryEntry> _equipment = new(StringComparer.Ordinal);

    public void Add(string id, string kind = "turnstile", string status = "active")
        => _equipment[id] = new EquipmentDirectoryEntry(id, kind, status);

    public Task<EquipmentDirectoryEntry?> GetEquipmentAsync(string equipmentId, CancellationToken cancellationToken = default)
        => Task.FromResult(_equipment.TryGetValue(equipmentId, out var entry) ? entry : null);
}
