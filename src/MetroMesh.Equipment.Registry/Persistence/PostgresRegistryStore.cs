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
                CHECK (status IN ('active', 'inactive', 'decommissioned')),
            created_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS ix_registry_equipment_station
            ON registry_equipment (station_id);

        CREATE TABLE IF NOT EXISTS registry_outbox (
            id text PRIMARY KEY,
            event_type text NOT NULL,
            aggregate_id text NOT NULL,
            envelope_json jsonb NOT NULL,
            published_at timestamptz NULL,
            attempts integer NOT NULL DEFAULT 0,
            last_error text NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE INDEX IF NOT EXISTS ix_registry_outbox_pending
            ON registry_outbox (created_at)
            WHERE published_at IS NULL;
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

    public async Task<StationRecord?> UpdateStationStatusAsync(string stationId, string status, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE registry_stations
            SET status = @status
            WHERE id = @id
            RETURNING id, name, address, status, location, created_at;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", stationId);
        command.Parameters.AddWithValue("status", status);

        StationRecord? station;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            station = await reader.ReadAsync(cancellationToken) ? ReadStation(reader) : null;
        }

        if (station is not null && status == "closed")
        {
            const string cascade = """
                UPDATE registry_equipment
                SET status = 'decommissioned'
                WHERE station_id = @id AND status = 'active';
                """;
            await using var cascadeCommand = new NpgsqlCommand(cascade, connection);
            cascadeCommand.Parameters.AddWithValue("id", stationId);
            await cascadeCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        return station;
    }

    public async Task<EquipmentRecord?> UpdateEquipmentStatusAsync(string equipmentId, string status, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE registry_equipment
            SET status = @status
            WHERE id = @id
            RETURNING id, kind, model, station_id, install_date, maintenance_threshold, status, created_at;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", equipmentId);
        command.Parameters.AddWithValue("status", status);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadEquipment(reader) : null;
    }

    public async Task<IReadOnlyList<EquipmentRecord>> ListEquipmentByStationAsync(string stationId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, kind, model, station_id, install_date, maintenance_threshold, status, created_at
            FROM registry_equipment WHERE station_id = @id ORDER BY created_at;
            """;

        var items = new List<EquipmentRecord>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", stationId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadEquipment(reader));
        }

        return items;
    }

    public async Task InsertOutboxEventAsync(string eventType, string aggregateId, string envelopeJson, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO registry_outbox (id, event_type, aggregate_id, envelope_json)
            VALUES (@id, @eventType, @aggregateId, @envelopeJson::jsonb);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", $"out_{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("aggregateId", aggregateId);
        command.Parameters.Add(new NpgsqlParameter("envelopeJson", NpgsqlTypes.NpgsqlDbType.Jsonb) { Value = envelopeJson });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxRecord>> GetUnpublishedOutboxAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, event_type, aggregate_id, envelope_json::text, attempts
            FROM registry_outbox
            WHERE published_at IS NULL
            ORDER BY created_at
            LIMIT @batchSize;
            """;

        var items = new List<OutboxRecord>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("batchSize", batchSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new OutboxRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4)));
        }

        return items;
    }

    public async Task MarkOutboxPublishedAsync(string outboxId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE registry_outbox SET published_at = now() WHERE id = @id;";
        await ExecuteSimpleAsync(sql, outboxId, null, cancellationToken);
    }

    public async Task MarkOutboxFailedAsync(string outboxId, string error, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE registry_outbox SET attempts = attempts + 1, last_error = @error WHERE id = @id;";
        await ExecuteSimpleAsync(sql, outboxId, error, cancellationToken);
    }

    private async Task ExecuteSimpleAsync(string sql, string id, string? error, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        if (error is not null)
        {
            command.Parameters.AddWithValue("error", error);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
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
