using MetroMesh.Telemetry.Core.Contracts;
using Npgsql;
using NpgsqlTypes;

namespace MetroMesh.Telemetry.Core.Persistence;

public sealed class PostgresTelemetryEventStore : ITelemetryEventStore
{
    private const string InitializeSql = """
        CREATE TABLE IF NOT EXISTS telemetry_events (
            event_id text PRIMARY KEY,
            correlation_id text NOT NULL,
            event_type text NOT NULL,
            source text NOT NULL,
            subject text NOT NULL,
            envelope_json jsonb NOT NULL,
            status text NOT NULL DEFAULT 'accepted'
                CHECK (status IN ('accepted', 'published', 'processed', 'dead_lettered')),
            attempts integer NOT NULL DEFAULT 0,
            last_error text NULL,
            accepted_at timestamptz NOT NULL DEFAULT now(),
            published_at timestamptz NULL,
            processed_at timestamptz NULL,
            dead_lettered_at timestamptz NULL
        );

        CREATE INDEX IF NOT EXISTS ix_telemetry_events_pending
            ON telemetry_events (accepted_at)
            WHERE published_at IS NULL;
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PostgresTelemetryEventStore(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(InitializeSql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task InsertAcceptedAsync(TelemetryEventEnvelope envelope, string envelopeJson, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO telemetry_events (
                event_id, correlation_id, event_type, source, subject,
                envelope_json, status, accepted_at)
            VALUES (
                @eventId, @correlationId, @eventType, @source, @subject,
                @envelopeJson::jsonb, 'accepted', @acceptedAt)
            ON CONFLICT (event_id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", envelope.Id);
        command.Parameters.AddWithValue("correlationId", envelope.CorrelationId);
        command.Parameters.AddWithValue("eventType", envelope.Type);
        command.Parameters.AddWithValue("source", envelope.Source);
        command.Parameters.AddWithValue("subject", envelope.Subject);
        command.Parameters.Add(new NpgsqlParameter("envelopeJson", NpgsqlDbType.Jsonb) { Value = envelopeJson });
        command.Parameters.AddWithValue("acceptedAt", envelope.Time);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TelemetryEventStatusResponse?> GetStatusAsync(string eventId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT event_id, status, accepted_at, published_at, processed_at,
                   dead_lettered_at, attempts, last_error
            FROM telemetry_events
            WHERE event_id = @eventId;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TelemetryEventStatusResponse(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetFieldValue<DateTimeOffset>(2),
            reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
            reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
            reader.GetInt32(6),
            reader.IsDBNull(7) ? null : reader.GetString(7));
    }

    public async Task<IReadOnlyList<StoredTelemetryEvent>> GetUnpublishedAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT event_id, envelope_json::text, attempts, correlation_id
            FROM telemetry_events
            WHERE status = 'accepted' AND published_at IS NULL
            ORDER BY accepted_at
            LIMIT @batchSize;
            """;

        var events = new List<StoredTelemetryEvent>();
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("batchSize", batchSize);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            events.Add(new StoredTelemetryEvent(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.IsDBNull(3) ? null : reader.GetString(3)));
        }

        return events;
    }

    public async Task MarkPublishedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE telemetry_events
            SET status = 'published', published_at = now()
            WHERE event_id = @eventId AND published_at IS NULL;
            """;

        await ExecuteAsync(sql, eventId, null, cancellationToken);
    }

    public async Task MarkPublishFailedAsync(string eventId, string error, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE telemetry_events
            SET attempts = attempts + 1, last_error = @error
            WHERE event_id = @eventId AND published_at IS NULL;
            """;

        await ExecuteAsync(sql, eventId, error, cancellationToken);
    }

    public async Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE telemetry_events
            SET status = 'processed', processed_at = now()
            WHERE event_id = @eventId
              AND processed_at IS NULL
              AND status IN ('accepted', 'published');
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);
        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    private async Task ExecuteAsync(string sql, string eventId, string? error, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);
        if (error is not null)
        {
            command.Parameters.AddWithValue("error", error);
        }

        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
