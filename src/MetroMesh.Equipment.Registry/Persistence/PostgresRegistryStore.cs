using Npgsql;

namespace MetroMesh.Equipment.Registry.Persistence;

public sealed class PostgresRegistryStore : IRegistryStore
{
    private const string InitializeSql = """
        CREATE TABLE IF NOT EXISTS registry_stations (
            id text PRIMARY KEY,
            name text NOT NULL,
            address text NOT NULL,
            status text NOT NULL DEFAULT 'open'
                CHECK (status IN ('open', 'closed', 'offline')),
            location text NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS registry_equipment (
            id text PRIMARY KEY,
            kind text NOT NULL CHECK (kind IN ('turnstile', 'camera')),
            model text NOT NULL,
            station_id text NOT NULL REFERENCES registry_stations(id),
            install_date date NULL,
            maintenance_threshold integer NULL,
            status text NOT NULL DEFAULT 'active'
                CHECK (status IN ('active', 'inactive')),
            created_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS ix_registry_equipment_station
            ON registry_equipment (station_id);
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresRegistryStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(InitializeSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
        return true;
    }

    public async Task<StationRecord> InsertStationAsync(string name, string address, string status, string? location, CancellationToken cancellationToken = default)
    {
        var id = $"st_{Guid.NewGuid():N}";
        const string sql = """
            INSERT INTO registry_stations (id, name, address, status, location)
            VALUES (@id, @name, @address, @status, @location)
            RETURNING id, name, address, status, location, created_at;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("address", address);
        command.Parameters.AddWithValue("status", status);
        command.Parameters.AddWithValue("location", (object?)location ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadStation(reader);
    }

    public async Task<StationRecord?> GetStationAsync(string stationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, name, address, status, location, created_at
            FROM registry_stations WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", stationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadStation(reader) : null;
    }

    public async Task<EquipmentRecord?> InsertEquipmentAsync(string kind, string model, string stationId, DateOnly? installDate, int? maintenanceThreshold, string status, CancellationToken cancellationToken = default)
    {
        var station = await GetStationAsync(stationId, cancellationToken);
        if (station is null)
        {
            return null;
        }

        var id = $"eq_{Guid.NewGuid():N}";
        const string sql = """
            INSERT INTO registry_equipment (id, kind, model, station_id, install_date, maintenance_threshold, status)
            VALUES (@id, @kind, @model, @stationId, @installDate, @threshold, @status)
            RETURNING id, kind, model, station_id, install_date, maintenance_threshold, status, created_at;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("kind", kind);
        command.Parameters.AddWithValue("model", model);
        command.Parameters.AddWithValue("stationId", stationId);
        command.Parameters.AddWithValue("installDate", (object?)installDate ?? DBNull.Value);
        command.Parameters.AddWithValue("threshold", (object?)maintenanceThreshold ?? DBNull.Value);
        command.Parameters.AddWithValue("status", status);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadEquipment(reader);
    }

    public async Task<EquipmentRecord?> GetEquipmentAsync(string equipmentId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, kind, model, station_id, install_date, maintenance_threshold, status, created_at
            FROM registry_equipment WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", equipmentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadEquipment(reader) : null;
    }

    private static StationRecord ReadStation(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetFieldValue<DateTimeOffset>(5));

    private static EquipmentRecord ReadEquipment(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetFieldValue<DateOnly>(4),
        reader.IsDBNull(5) ? null : reader.GetInt32(5),
        reader.GetString(6),
        reader.GetFieldValue<DateTimeOffset>(7));
}
