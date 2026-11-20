using MetroMesh.Identification.Review.Contracts;
using Npgsql;

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
            recorded_at timestamptz NOT NULL DEFAULT now()
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
                   confidence, passenger_id, offender_id, redacted
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
}
