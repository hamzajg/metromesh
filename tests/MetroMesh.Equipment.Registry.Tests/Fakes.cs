using System.Collections.Concurrent;
using MetroMesh.Equipment.Registry.Persistence;

namespace MetroMesh.Equipment.Registry.Tests;

public sealed class FakeRegistryStore : IRegistryStore
{
    private readonly ConcurrentDictionary<string, StationRecord> _stations = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, EquipmentRecord> _equipment = new(StringComparer.Ordinal);

    public bool PingOk { get; set; } = true;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(PingOk);

    public Task<StationRecord> InsertStationAsync(string name, string address, string status, string? location, CancellationToken cancellationToken = default)
    {
        var record = new StationRecord(
            $"st_{Guid.NewGuid():N}",
            name,
            address,
            status,
            location,
            DateTimeOffset.UtcNow);
        _stations[record.Id] = record;
        return Task.FromResult(record);
    }

    public Task<StationRecord?> GetStationAsync(string stationId, CancellationToken cancellationToken = default)
        => Task.FromResult(_stations.TryGetValue(stationId, out var s) ? s : null);

    public Task<EquipmentRecord?> InsertEquipmentAsync(string kind, string model, string stationId, DateOnly? installDate, int? maintenanceThreshold, string status, CancellationToken cancellationToken = default)
    {
        if (!_stations.ContainsKey(stationId))
        {
            return Task.FromResult<EquipmentRecord?>(null);
        }

        var record = new EquipmentRecord(
            $"eq_{Guid.NewGuid():N}",
            kind,
            model,
            stationId,
            installDate,
            maintenanceThreshold,
            status,
            DateTimeOffset.UtcNow);
        _equipment[record.Id] = record;
        return Task.FromResult<EquipmentRecord?>(record);
    }

    public Task<EquipmentRecord?> GetEquipmentAsync(string equipmentId, CancellationToken cancellationToken = default)
        => Task.FromResult(_equipment.TryGetValue(equipmentId, out var e) ? e : null);

    public Task<StationRecord?> UpdateStationStatusAsync(string stationId, string status, CancellationToken cancellationToken = default)
    {
        if (!_stations.TryGetValue(stationId, out var station))
        {
            return Task.FromResult<StationRecord?>(null);
        }

        var updated = station with { Status = status };
        _stations[stationId] = updated;

        if (status == "closed")
        {
            foreach (var (id, eq) in _equipment)
            {
                if (eq.StationId == stationId && eq.Status == "active")
                {
                    _equipment[id] = eq with { Status = "decommissioned" };
                }
            }
        }

        return Task.FromResult<StationRecord?>(updated);
    }

    public Task<EquipmentRecord?> UpdateEquipmentStatusAsync(string equipmentId, string status, CancellationToken cancellationToken = default)
    {
        if (!_equipment.TryGetValue(equipmentId, out var equipment))
        {
            return Task.FromResult<EquipmentRecord?>(null);
        }

        var updated = equipment with { Status = status };
        _equipment[equipmentId] = updated;
        return Task.FromResult<EquipmentRecord?>(updated);
    }

    public Task<IReadOnlyList<EquipmentRecord>> ListEquipmentByStationAsync(string stationId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<EquipmentRecord>>(
            _equipment.Values.Where(e => e.StationId == stationId).OrderBy(e => e.CreatedAt).ToList());
}
