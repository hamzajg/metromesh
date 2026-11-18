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
                assignee text NULL,
                resolution text NULL,
                status text NOT NULL DEFAULT 'open'
                    CHECK (status IN ('open', 'assigned', 'in_progress', 'completed', 'cancelled')),
                created_at timestamptz NOT NULL DEFAULT now(),
                updated_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS ix_work_orders_equipment
                ON work_orders (equipment_id);
            CREATE INDEX IF NOT EXISTS ix_work_orders_status
                ON work_orders (status);

            CREATE TABLE IF NOT EXISTS work_order_history (
                id text PRIMARY KEY,
                work_order_id text NOT NULL REFERENCES work_orders (id),
                from_status text NOT NULL,
                to_status text NOT NULL,
                note text NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS ix_work_order_history_order
                ON work_order_history (work_order_id, created_at);

            CREATE TABLE IF NOT EXISTS maintenance_outbox (
                id text PRIMARY KEY,
                event_type text NOT NULL,
                aggregate_id text NOT NULL,
                envelope_json jsonb NOT NULL,
                published_at timestamptz NULL,
                attempts integer NOT NULL DEFAULT 0,
                last_error text NULL,
                created_at timestamptz NOT NULL DEFAULT now()
            );

            CREATE INDEX IF NOT EXISTS ix_maintenance_outbox_pending
                ON maintenance_outbox (created_at)
                WHERE published_at IS NULL;
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
            RETURNING id, request_id, equipment_id, type, priority, title, description, assignee, resolution, status, created_at, updated_at;
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
            SELECT id, request_id, equipment_id, type, priority, title, description, assignee, resolution, status, created_at, updated_at
            FROM work_orders WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", workOrderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadWorkOrder(reader) : null;
    }

    public async Task<WorkOrderRecord?> TransitionWorkOrderAsync(string workOrderId, string toStatus, string? assignee, string? resolution, string? note, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string selectSql = """
            SELECT status FROM work_orders WHERE id = @id FOR UPDATE;
            """;
        string? fromStatus;
        await using (var select = new NpgsqlCommand(selectSql, connection, transaction))
        {
            select.Parameters.AddWithValue("id", workOrderId);
            var result = await select.ExecuteScalarAsync(cancellationToken);
            if (result is null)
            {
                return null;
            }

            fromStatus = (string)result;
        }

        const string updateSql = """
            UPDATE work_orders
            SET status = @toStatus,
                assignee = COALESCE(@assignee, assignee),
                resolution = COALESCE(@resolution, resolution),
                updated_at = now()
            WHERE id = @id
            RETURNING id, request_id, equipment_id, type, priority, title, description, assignee, resolution, status, created_at, updated_at;
            """;
        WorkOrderRecord? updated;
        await using (var update = new NpgsqlCommand(updateSql, connection, transaction))
        {
            update.Parameters.AddWithValue("id", workOrderId);
            update.Parameters.AddWithValue("toStatus", toStatus);
            update.Parameters.Add(new NpgsqlParameter("assignee", NpgsqlDbType.Text) { Value = (object?)assignee ?? DBNull.Value });
            update.Parameters.Add(new NpgsqlParameter("resolution", NpgsqlDbType.Text) { Value = (object?)resolution ?? DBNull.Value });

            await using var reader = await update.ExecuteReaderAsync(cancellationToken);
            updated = await reader.ReadAsync(cancellationToken) ? ReadWorkOrder(reader) : null;
        }

        const string historySql = """
            INSERT INTO work_order_history (id, work_order_id, from_status, to_status, note)
            VALUES (@id, @workOrderId, @fromStatus, @toStatus, @note);
            """;
        await using (var history = new NpgsqlCommand(historySql, connection, transaction))
        {
            history.Parameters.AddWithValue("id", $"wh_{Guid.NewGuid():N}");
            history.Parameters.AddWithValue("workOrderId", workOrderId);
            history.Parameters.AddWithValue("fromStatus", fromStatus!);
            history.Parameters.AddWithValue("toStatus", toStatus);
            history.Parameters.Add(new NpgsqlParameter("note", NpgsqlDbType.Text) { Value = (object?)note ?? DBNull.Value });
            await history.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return updated;
    }

    public async Task<IReadOnlyList<WorkOrderHistoryRecord>> ListWorkOrderHistoryAsync(string workOrderId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, work_order_id, from_status, to_status, note, created_at
            FROM work_order_history
            WHERE work_order_id = @workOrderId
            ORDER BY created_at, id;
            """;

        var items = new List<WorkOrderHistoryRecord>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workOrderId", workOrderId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new WorkOrderHistoryRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return items;
    }

    public async Task<IReadOnlyList<WorkOrderRecord>> ListWorkOrdersAsync(string? status, string? equipmentId, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT id, request_id, equipment_id, type, priority, title, description, assignee, resolution, status, created_at, updated_at
            FROM work_orders
            WHERE (@status IS NULL OR status = @status)
              AND (@equipmentId IS NULL OR equipment_id = @equipmentId)
            ORDER BY created_at DESC;
            """;

        var items = new List<WorkOrderRecord>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Text) { Value = (object?)status ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("equipmentId", NpgsqlDbType.Text) { Value = (object?)equipmentId ?? DBNull.Value });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadWorkOrder(reader));
        }

        return items;
    }

    public async Task<IReadOnlyList<MaintenanceRequestRecord>> ListRequestsAsync(string? status, string? equipmentId, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT id, equipment_id, reason, severity, source, details, status, created_at
            FROM maintenance_requests
            WHERE (@status IS NULL OR status = @status)
              AND (@equipmentId IS NULL OR equipment_id = @equipmentId)
            ORDER BY created_at DESC;
            """;

        var items = new List<MaintenanceRequestRecord>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Text) { Value = (object?)status ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter("equipmentId", NpgsqlDbType.Text) { Value = (object?)equipmentId ?? DBNull.Value });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(ReadRequest(reader));
        }

        return items;
    }

    public async Task UpdateRequestStatusAsync(string requestId, string status, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE maintenance_requests SET status = @status WHERE id = @id;";
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", requestId);
        command.Parameters.AddWithValue("status", status);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InsertOutboxEventAsync(string eventType, string aggregateId, string envelopeJson, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO maintenance_outbox (id, event_type, aggregate_id, envelope_json)
            VALUES (@id, @eventType, @aggregateId, @envelopeJson::jsonb);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", $"out_{Guid.NewGuid():N}");
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("aggregateId", aggregateId);
        command.Parameters.Add(new NpgsqlParameter("envelopeJson", NpgsqlDbType.Jsonb) { Value = envelopeJson });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxRecord>> GetUnpublishedOutboxAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, event_type, aggregate_id, envelope_json::text, attempts
            FROM maintenance_outbox
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
        const string sql = "UPDATE maintenance_outbox SET published_at = now() WHERE id = @id;";
        await ExecuteSimpleAsync(sql, outboxId, null, cancellationToken);
    }

    public async Task MarkOutboxFailedAsync(string outboxId, string error, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE maintenance_outbox SET attempts = attempts + 1, last_error = @error WHERE id = @id;";
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
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.GetString(9),
        reader.GetFieldValue<DateTimeOffset>(10),
        reader.GetFieldValue<DateTimeOffset>(11));
}
