using MetroMesh.Analytics.Contracts;
using Npgsql;

namespace MetroMesh.Analytics.Persistence;

public sealed class AnalyticsStore : IAnalyticsStore
{
    private const string InitializeSql = """
        CREATE TABLE IF NOT EXISTS analytics_processed (
            event_id text PRIMARY KEY,
            processed_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS ridership_readings (
            event_id text PRIMARY KEY,
            station_id text NOT NULL,
            equipment_id text NOT NULL,
            event_type text NOT NULL,
            occurred_at timestamptz NOT NULL,
            passenger_count integer NOT NULL DEFAULT 1,
            recorded_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS processing_failures (
            event_id text PRIMARY KEY,
            attempts integer NOT NULL DEFAULT 0,
            last_error text NULL,
            dead_lettered_at timestamptz NULL
        );
        """;

    private readonly NpgsqlDataSource _dataSource;

    public AnalyticsStore(NpgsqlDataSource dataSource)
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
            INSERT INTO analytics_processed (event_id)
            VALUES (@eventId)
            ON CONFLICT (event_id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);
        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task RecordReadingAsync(RidershipReading reading, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO ridership_readings (
                event_id, station_id, equipment_id, event_type, occurred_at, passenger_count)
            VALUES (
                @eventId, @stationId, @equipmentId, @eventType, @occurredAt, @passengerCount)
            ON CONFLICT (event_id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", reading.EventId);
        command.Parameters.AddWithValue("stationId", reading.StationId);
        command.Parameters.AddWithValue("equipmentId", reading.EquipmentId);
        command.Parameters.AddWithValue("eventType", reading.EventType);
        command.Parameters.AddWithValue("occurredAt", reading.OccurredAt);
        command.Parameters.AddWithValue("passengerCount", reading.PassengerCount);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> RecordProcessingFailureAsync(string eventId, string error, int maxAttempts, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO processing_failures (event_id, attempts, last_error)
            VALUES (@eventId, 1, @error)
            ON CONFLICT (event_id)
            DO UPDATE SET
                attempts = processing_failures.attempts + 1,
                last_error = @error,
                dead_lettered_at = CASE
                    WHEN processing_failures.attempts + 1 >= @maxAttempts THEN now()
                    ELSE processing_failures.dead_lettered_at
                END
            RETURNING attempts, dead_lettered_at;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);
        command.Parameters.AddWithValue("error", error);
        command.Parameters.AddWithValue("maxAttempts", maxAttempts);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return false;
        }

        var attempts = reader.GetInt32(0);
        var deadLetteredAt = reader.IsDBNull(1) ? (DateTime?)null : reader.GetDateTime(1);
        return deadLetteredAt is not null || attempts >= maxAttempts;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<RidershipCount>> GetCountsAsync(string? stationId, string? equipmentId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        var sql = """
            SELECT station_id, equipment_id, COUNT(*) AS count
            FROM ridership_readings
            WHERE (@stationId IS NULL OR station_id = @stationId)
              AND (@equipmentId IS NULL OR equipment_id = @equipmentId)
              AND (@from IS NULL OR occurred_at >= @from)
              AND (@to IS NULL OR occurred_at <= @to)
            GROUP BY station_id, equipment_id
            ORDER BY station_id, equipment_id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("stationId", (object)stationId ?? DBNull.Value);
        command.Parameters.AddWithValue("equipmentId", (object)equipmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("from", (object)from ?? DBNull.Value);
        command.Parameters.AddWithValue("to", (object)to ?? DBNull.Value);

        var results = new List<RidershipCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new RidershipCount(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetInt32(2)));
        }

        return results;
    }

    public async Task<IReadOnlyList<TimeBucketCount>> GetSeriesAsync(string? stationId, string? equipmentId, string bucket, DateTimeOffset? from, DateTimeOffset? to, CancellationToken cancellationToken = default)
    {
        var bucketExpr = bucket switch
        {
            "hour" => "date_trunc('hour', occurred_at)",
            "day" => "date_trunc('day', occurred_at)",
            _ => "date_trunc('hour', occurred_at)"
        };

        var sql = $$"""
            SELECT {{bucketExpr}} AS bucket_start, COUNT(*) AS count
            FROM ridership_readings
            WHERE (@stationId IS NULL OR station_id = @stationId)
              AND (@equipmentId IS NULL OR equipment_id = @equipmentId)
              AND (@from IS NULL OR occurred_at >= @from)
              AND (@to IS NULL OR occurred_at <= @to)
            GROUP BY bucket_start
            ORDER BY bucket_start;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("stationId", (object)stationId ?? DBNull.Value);
        command.Parameters.AddWithValue("equipmentId", (object)equipmentId ?? DBNull.Value);
        command.Parameters.AddWithValue("from", (object)from ?? DBNull.Value);
        command.Parameters.AddWithValue("to", (object)to ?? DBNull.Value);

        var results = new List<TimeBucketCount>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new TimeBucketCount(
                reader.GetDateTime(0),
                reader.GetInt32(1)));
        }

        return results;
    }
}
