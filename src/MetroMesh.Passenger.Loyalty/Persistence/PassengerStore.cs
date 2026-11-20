using MetroMesh.Passenger.Loyalty.Contracts;
using Npgsql;

namespace MetroMesh.Passenger.Loyalty.Persistence;

public sealed class PassengerStore : IPassengerStore
{
    private const string InitializeSql = """
        CREATE TABLE IF NOT EXISTS passenger_processed (
            event_id text PRIMARY KEY,
            processed_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS passenger_profiles (
            id text PRIMARY KEY,
            profile_reference text NOT NULL,
            loyalty_program_reference text NULL,
            status text NOT NULL DEFAULT 'active',
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS passenger_info_history (
            id text PRIMARY KEY,
            passenger_id text NOT NULL,
            contact_email text NOT NULL,
            contact_phone text NULL,
            address text NULL,
            updated_at timestamptz NOT NULL DEFAULT now(),
            previous_contact_email text NULL,
            previous_contact_phone text NULL,
            previous_address text NULL
        );

        CREATE TABLE IF NOT EXISTS loyalty_programs (
            id text PRIMARY KEY,
            passenger_id text NOT NULL,
            tier text NOT NULL,
            status text NOT NULL DEFAULT 'active',
            terms text NULL,
            created_at timestamptz NOT NULL DEFAULT now(),
            updated_at timestamptz NOT NULL DEFAULT now(),
            retired_at timestamptz NULL
        );

        CREATE TABLE IF NOT EXISTS passenger_notifications (
            id text PRIMARY KEY,
            passenger_id text NOT NULL,
            type text NOT NULL,
            recipient text NOT NULL,
            status text NOT NULL DEFAULT 'pending',
            created_at timestamptz NOT NULL DEFAULT now()
        );

        CREATE TABLE IF NOT EXISTS passenger_audit (
            id text PRIMARY KEY,
            passenger_id text NOT NULL,
            action text NOT NULL,
            actor text NULL,
            details text NULL,
            created_at timestamptz NOT NULL DEFAULT now()
        );
        """;

    private readonly NpgsqlDataSource _dataSource;

    public PassengerStore(NpgsqlDataSource dataSource)
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
            INSERT INTO passenger_processed (event_id)
            VALUES (@eventId)
            ON CONFLICT (event_id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("eventId", eventId);
        var rows = await command.ExecuteNonQueryAsync(cancellationToken);
        return rows > 0;
    }

    public async Task RecordPassengerAsync(PassengerProfile profile, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO passenger_profiles (
                id, profile_reference, loyalty_program_reference, status, created_at, updated_at)
            VALUES (
                @id, @profileReference, @loyaltyProgramReference, @status, @createdAt, @updatedAt)
            ON CONFLICT (id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", profile.Id);
        command.Parameters.AddWithValue("profileReference", profile.ProfileReference);
        command.Parameters.AddWithValue("loyaltyProgramReference", (object)profile.LoyaltyProgramReference ?? DBNull.Value);
        command.Parameters.AddWithValue("status", profile.Status);
        command.Parameters.AddWithValue("createdAt", profile.CreatedAt);
        command.Parameters.AddWithValue("updatedAt", profile.UpdatedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PassengerProfile?> GetPassengerAsync(string passengerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, profile_reference, loyalty_program_reference, status, created_at, updated_at
            FROM passenger_profiles
            WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", passengerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PassengerProfile(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            reader.GetString(3),
            reader.GetDateTime(4),
            reader.GetDateTime(5));
    }

    public async Task RecordPassengerInfoAsync(PassengerInfoRecord info, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO passenger_info_history (
                id, passenger_id, contact_email, contact_phone, address, updated_at,
                previous_contact_email, previous_contact_phone, previous_address)
            VALUES (
                @id, @passengerId, @contactEmail, @contactPhone, @address, @updatedAt,
                @previousContactEmail, @previousContactPhone, @previousAddress);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", info.Id);
        command.Parameters.AddWithValue("passengerId", info.PassengerId);
        command.Parameters.AddWithValue("contactEmail", info.ContactEmail);
        command.Parameters.AddWithValue("contactPhone", (object)info.ContactPhone ?? DBNull.Value);
        command.Parameters.AddWithValue("address", (object)info.Address ?? DBNull.Value);
        command.Parameters.AddWithValue("updatedAt", info.UpdatedAt);
        command.Parameters.AddWithValue("previousContactEmail", (object)info.PreviousContactEmail ?? DBNull.Value);
        command.Parameters.AddWithValue("previousContactPhone", (object)info.PreviousContactPhone ?? DBNull.Value);
        command.Parameters.AddWithValue("previousAddress", (object)info.PreviousAddress ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PassengerInfoRecord>> GetPassengerInfoAsync(string passengerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, passenger_id, contact_email, contact_phone, address, updated_at,
                   previous_contact_email, previous_contact_phone, previous_address
            FROM passenger_info_history
            WHERE passenger_id = @passengerId
            ORDER BY updated_at DESC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("passengerId", passengerId);

        var results = new List<PassengerInfoRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new PassengerInfoRecord(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetDateTime(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }

        return results;
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand("SELECT 1", connection);
        await command.ExecuteScalarAsync(cancellationToken);
        return true;
    }

    public async Task RecordLoyaltyProgramAsync(LoyaltyProgram program, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO loyalty_programs (
                id, passenger_id, tier, status, terms, created_at, updated_at, retired_at)
            VALUES (
                @id, @passengerId, @tier, @status, @terms, @createdAt, @updatedAt, @retiredAt)
            ON CONFLICT (id) DO NOTHING;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", program.Id);
        command.Parameters.AddWithValue("passengerId", program.PassengerId);
        command.Parameters.AddWithValue("tier", program.Tier);
        command.Parameters.AddWithValue("status", program.Status);
        command.Parameters.AddWithValue("terms", (object)program.Terms ?? DBNull.Value);
        command.Parameters.AddWithValue("createdAt", program.CreatedAt);
        command.Parameters.AddWithValue("updatedAt", program.UpdatedAt);
        command.Parameters.AddWithValue("retiredAt", (object)program.RetiredAt ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LoyaltyProgram?> GetLoyaltyProgramAsync(string programId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, passenger_id, tier, status, terms, created_at, updated_at, retired_at
            FROM loyalty_programs
            WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", programId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new LoyaltyProgram(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.IsDBNull(4) ? null : reader.GetString(4),
            reader.GetDateTime(5),
            reader.GetDateTime(6),
            reader.IsDBNull(7) ? (DateTimeOffset?)null : reader.GetDateTime(7));
    }

    public async Task<IReadOnlyList<LoyaltyProgram>> GetLoyaltyProgramsAsync(string passengerId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT id, passenger_id, tier, status, terms, created_at, updated_at, retired_at
            FROM loyalty_programs
            WHERE passenger_id = @passengerId
            ORDER BY created_at DESC;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("passengerId", passengerId);

        var results = new List<LoyaltyProgram>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new LoyaltyProgram(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                reader.GetDateTime(5),
                reader.GetDateTime(6),
                reader.IsDBNull(7) ? (DateTimeOffset?)null : reader.GetDateTime(7)));
        }

        return results;
    }

    public async Task RetireLoyaltyProgramAsync(string programId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            UPDATE loyalty_programs
            SET status = 'retired', retired_at = @retiredAt, updated_at = @updatedAt
            WHERE id = @id;
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", programId);
        command.Parameters.AddWithValue("retiredAt", DateTimeOffset.UtcNow);
        command.Parameters.AddWithValue("updatedAt", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task RecordNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        const string sql = """
            INSERT INTO passenger_notifications (id, passenger_id, type, recipient, status)
            VALUES (@id, @passengerId, @type, @recipient, @status);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", notification.Id);
        command.Parameters.AddWithValue("passengerId", notification.PassengerId);
        command.Parameters.AddWithValue("type", notification.Type);
        command.Parameters.AddWithValue("recipient", notification.Recipient);
        command.Parameters.AddWithValue("status", notification.Status);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Notification>> GetNotificationsAsync(string? passengerId, CancellationToken cancellationToken = default)
    {
        var sql = passengerId is not null
            ? """
                SELECT id, passenger_id, type, recipient, status, created_at
                FROM passenger_notifications
                WHERE passenger_id = @passengerId
                ORDER BY created_at;
                """
            : """
                SELECT id, passenger_id, type, recipient, status, created_at
                FROM passenger_notifications
                ORDER BY created_at;
                """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        if (passengerId is not null)
        {
            command.Parameters.AddWithValue("passengerId", passengerId);
        }

        var results = new List<Notification>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new Notification(
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
            INSERT INTO passenger_audit (id, passenger_id, action, actor, details)
            VALUES (@id, @passengerId, @action, @actor, @details);
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", entry.Id);
        command.Parameters.AddWithValue("passengerId", entry.PassengerId);
        command.Parameters.AddWithValue("action", entry.Action);
        command.Parameters.AddWithValue("actor", (object)entry.Actor ?? DBNull.Value);
        command.Parameters.AddWithValue("details", (object)entry.Details ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditEntry>> GetAuditAsync(string? passengerId, CancellationToken cancellationToken = default)
    {
        var sql = passengerId is not null
            ? """
                SELECT id, passenger_id, action, actor, details, created_at
                FROM passenger_audit
                WHERE passenger_id = @passengerId
                ORDER BY created_at;
                """
            : """
                SELECT id, passenger_id, action, actor, details, created_at
                FROM passenger_audit
                ORDER BY created_at;
                """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        if (passengerId is not null)
        {
            command.Parameters.AddWithValue("passengerId", passengerId);
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
}
