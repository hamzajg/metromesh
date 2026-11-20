using MetroMesh.Identification.Review.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace MetroMesh.Identification.Review.Persistence;

public sealed class IdentificationStore : IIdentificationStore
{
    private const string InitializeSql = """
        CREATE TABLE IF NOT EXISTS identification_processed (
            event_id text PRIMARY KEY,
            processed_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS customer_station_interactions (
            id text PRIMARY KEY,
            station_id text NOT NULL,
            turnstile_id text NOT NULL,
            event_type text NOT NULL,
            occurred_at timestamptz NOT NULL,
            match_result text NOT NULL DEFAULT 'none',
            confidence double precision NULL,
            passenger_id text NULL,
            offender_id text NULL,
            redacted text NULL,
            reviewed text NULL,
            reviewed_by text NULL,
            reviewed_at timestamptz NULL,
            recorded_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS identification_alerts (
            id text PRIMARY KEY,
            interaction_id text NOT NULL,
            match_result text NOT NULL,
            recipient text NOT NULL,
            status text NOT NULL DEFAULT 'pending',
            created_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS identification_audit (
            id text PRIMARY KEY,
            interaction_id text NOT NULL,
            action text NOT NULL,
            actor text NULL,
            details text NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        """;

    private readonly NpgsqlDataSource _dataSource;

    public IdentificationStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(InitializeSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO identification_processed (event_id)
            VALUES (@eventId)
            ON CONFLICT (event_id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);
        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task RecordInteractionAsync(CustomerStationInteraction interaction, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO customer_station_interactions (
                id, station_id, turnstile_id, event_type, occurred_at, match_result, confidence,
                passenger_id, offender_id, redacted)
            VALUES (
                @id, @stationId, @turnstileId, @eventType, @occurredAt, @matchResult, @confidence,
                @passengerId, @offenderId, @redacted)
            ON CONFLICT (id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", interaction.Id);
        command.Parameters.AddWithValue("stationId", interaction.StationId);
        command.Parameters.AddWithValue("turnstileId", interaction.TurnstileId);
        command.Parameters.AddWithValue("eventType", interaction.EventType);
        command.Parameters.AddWithValue("occurredAt", interaction.OccurredAt);
        command.Parameters.AddWithValue("matchResult", interaction.MatchResult);
        command.Parameters.AddWithValue("confidence", (object)interaction.Confidence ?? DBNull.Value);
        command.Parameters.AddWithValue("passengerId", (object)interaction.PassengerId ?? DBNull.Value);
        command.Parameters.AddWithValue("offenderId", (object)interaction.OffenderId ?? DBNull.Value);
        command.Parameters.AddWithValue("redacted", (object)interaction.Redacted ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CustomerStationInteraction?> GetInteractionAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, station_id, turnstile_id, event_type, occurred_at, match_result,
                   confidence, passenger_id, offender_id, redacted, reviewed, reviewed_by, reviewed_at
            FROM customer_station_interactions
            WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", interactionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CustomerStationInteraction(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetDateTime(4),
            reader.GetString(5),
            reader.IsDBNull(6) ? (double?)null : reader.GetDouble(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9));
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
        return true;
    }

    public async Task RecordAlertAsync(AlertNotification alert, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO identification_alerts (id, interaction_id, match_result, recipient, status)
            VALUES (@id, @interactionId, @matchResult, @recipient, @status);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", alert.Id);
        command.Parameters.AddWithValue("interactionId", alert.InteractionId);
        command.Parameters.AddWithValue("matchResult", alert.MatchResult);
        command.Parameters.AddWithValue("recipient", alert.Recipient);
        command.Parameters.AddWithValue("status", alert.Status);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertNotification>> GetAlertsAsync(string? interactionId, CancellationToken cancellationToken = default)
    {
        var sql = interactionId is not null
            ? """
                SELECT id, interaction_id, match_result, recipient, status, created_at
                FROM identification_alerts
                WHERE interaction_id = @interactionId
                ORDER BY created_at;
                """
            : """
                SELECT id, interaction_id, match_result, recipient, status, created_at
                FROM identification_alerts
                ORDER BY created_at;
                """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        if (interactionId is not null)
        {
            command.Parameters.AddWithValue("interactionId", interactionId);
        }

        var results = new List<AlertNotification>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AlertNotification(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetDateTime(5)));
        }

        return results;
    }

    public async Task RecordAuditAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO identification_audit (id, interaction_id, action, actor, details)
            VALUES (@id, @interactionId, @action, @actor, @details);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", entry.Id);
        command.Parameters.AddWithValue("interactionId", entry.InteractionId);
        command.Parameters.AddWithValue("action", entry.Action);
        command.Parameters.AddWithValue("actor", (object)entry.Actor ?? DBNull.Value);
        command.Parameters.AddWithValue("details", (object)entry.Details ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAuditAsync(string? interactionId, CancellationToken cancellationToken = default)
    {
        var sql = interactionId is not null
            ? """
                SELECT id, interaction_id, action, actor, details, created_at
                FROM identification_audit
                WHERE interaction_id = @interactionId
                ORDER BY created_at;
                """
            : """
                SELECT id, interaction_id, action, actor, details, created_at
                FROM identification_audit
                ORDER BY created_at;
                """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        if (interactionId is not null)
        {
            command.Parameters.AddWithValue("interactionId", interactionId);
        }

        var results = new List<AuditEntry>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new AuditEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetDateTime(5)));
        }

        return results;
    }

    public async Task RecordReviewAsync(ReviewDecision decision, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE customer_station_interactions
            SET reviewed = @decision, reviewed_by = @reviewer, reviewed_at = @reviewedAt
            WHERE id = @interactionId;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("interactionId", decision.InteractionId);
        command.Parameters.AddWithValue("decision", decision.Decision);
        command.Parameters.AddWithValue("reviewer", decision.Reviewer);
        command.Parameters.AddWithValue("reviewedAt", decision.ReviewedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<ReviewDecision?> GetReviewAsync(string interactionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT reviewed, reviewed_by, reviewed_at
            FROM customer_station_interactions
            WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", interactionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || reader.IsDBNull(0))
        {
            return null;
        }

        return new ReviewDecision(
            interactionId,
            reader.GetString(0),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.GetDateTime(2));
    }

    public async Task<int> PurgeNonMatchOlderThanAsync(DateTimeOffset olderThan, string reason, CancellationToken cancellationToken = default)
    {
        const string selectSql = """
            SELECT id FROM customer_station_interactions
            WHERE match_result = 'none'
              AND occurred_at < @olderThan
              AND (reviewed IS NULL OR reviewed = 'none');
            """;

        var toPurge = new List<string>();
        await using var selectConnection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var selectCommand = new NpgsqlCommand(selectSql, selectConnection);
        selectCommand.Parameters.AddWithValue("olderThan", olderThan);
        await using var reader = await selectCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            toPurge.Add(reader.GetString(0));
        }

        if (toPurge.Count == 0)
        {
            return 0;
        }

        const string deleteSql = """
            DELETE FROM customer_station_interactions
            WHERE id = ANY(@ids);
            """;

        await using var deleteConnection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var deleteCommand = new NpgsqlCommand(deleteSql, deleteConnection);
        var idsParam = new NpgsqlParameter("ids", NpgsqlDbType.Text | NpgsqlDbType.Array)
        {
            Value = toPurge.ToArray()
        };
        deleteCommand.Parameters.Add(idsParam);

        return await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
