using Npgsql;
using NpgsqlTypes;

namespace MetroMesh.Maintenance.Persistence;

public sealed class PostgresMaintenanceStore : IMaintenanceStore
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresMaintenanceStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            CREATE TABLE IF NOT EXISTS maintenance_requests (
                id text PRIMARY KEY,
                equipment_id text NOT NULL,
                reason text NOT NULL,
                severity text NOT NULL
                    CHECK (severity IN ('low', 'medium', 'high', 'critical')),
                source text NOT NULL DEFAULT 'manual'
                    CHECK (source IN ('manual', 'signal')),
                details text NULL,
                status text NOT NULL DEFAULT 'open'
                    CHECK (status IN ('open', 'converted', 'cancelled')),
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS ix_maintenance_requests_equipment
                ON maintenance_requests (equipment_id);

            CREATE TABLE IF NOT EXISTS work_orders (
                id text PRIMARY KEY,
                request_id text NULL REFERENCES maintenance_requests (id),
                equipment_id text NOT NULL,
                type text NOT NULL
                    CHECK (type IN ('repair', 'inspection', 'upgrade', 'other')),
                priority text NOT NULL
                    CHECK (priority IN ('low', 'medium', 'high', 'urgent')),
                title text NOT NULL,
                description text NULL,
                status text NOT NULL DEFAULT 'open'
                    CHECK (status IN ('open', 'assigned', 'in_progress', 'completed', 'cancelled')),
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS ix_work_orders_equipment
                ON work_orders (equipment_id);
            CREATE INDEX IF NOT EXISTS ix_work_orders_status
                ON work_orders (status);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    public async Task<MaintenanceRequestRecord> InsertRequestAsync(string equipmentId, string reason, string severity, string source, string? details, CancellationToken cancellationToken = default)
    {
        var id = $"mr_{Guid.NewGuid():N}";
        const string sql = """
            INSERT INTO maintenance_requests (id, equipment_id, reason, severity, source, details)
            VALUES (@id, @equipmentId, @reason, @severity, @source, @details)
            RETURNING id, equipment_id, reason, severity, source, details, status, created_at;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("equipmentId", equipmentId);
        command.Parameters.AddWithValue("reason", reason);
        command.Parameters.AddWithValue("severity", severity.ToLowerInvariant());
        command.Parameters.AddWithValue("source", source.ToLowerInvariant());
        command.Parameters.Add(new NpgsqlParameter("details", NpgsqlDbType.Text) { Value = (object?)details ?? DBNull.Value });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadRequest(reader);
    }

    public async Task<MaintenanceRequestRecord?> GetRequestAsync(string requestId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, equipment_id, reason, severity, source, details, status, created_at
            FROM maintenance_requests WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", requestId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadRequest(reader) : null;
    }

    public async Task<WorkOrderRecord> InsertWorkOrderAsync(string? requestId, string equipmentId, string type, string priority, string title, string? description, CancellationToken cancellationToken = default)
    {
        var id = $"wo_{Guid.NewGuid():N}";
        const string sql = """
            INSERT INTO work_orders (id, request_id, equipment_id, type, priority, title, description)
            VALUES (@id, @requestId, @equipmentId, @type, @priority, @title, @description)
            RETURNING id, request_id, equipment_id, type, priority, title, description, status, created_at, updated_at;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.Add(new NpgsqlParameter("requestId", NpgsqlDbType.Text) { Value = (object?)requestId ?? DBNull.Value });
        command.Parameters.AddWithValue("equipmentId", equipmentId);
        command.Parameters.AddWithValue("type", type.ToLowerInvariant());
        command.Parameters.AddWithValue("priority", priority.ToLowerInvariant());
        command.Parameters.AddWithValue("title", title);
        command.Parameters.Add(new NpgsqlParameter("description", NpgsqlDbType.Text) { Value = (object?)description ?? DBNull.Value });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return ReadWorkOrder(reader);
    }

    public async Task<WorkOrderRecord?> GetWorkOrderAsync(string workOrderId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, request_id, equipment_id, type, priority, title, description, status, created_at, updated_at
            FROM work_orders WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", workOrderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadWorkOrder(reader) : null;
    }

    private static MaintenanceRequestRecord ReadRequest(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.GetString(6),
        reader.GetFieldValue<DateTimeOffset>(7));

    private static WorkOrderRecord ReadWorkOrder(NpgsqlDataReader reader) => new(
        reader.GetString(0),
        reader.IsDBNull(1) ? null : reader.GetString(1),
        reader.GetString(2),
        reader.GetString(3),
        reader.GetString(4),
        reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.GetString(7),
        reader.GetFieldValue<DateTimeOffset>(8),
        reader.GetFieldValue<DateTimeOffset>(9));
}
