using System.Text.Json;
using MetroMesh.Telemetry.Core.Contracts;
using MetroMesh.Telemetry.Core.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MetroMesh.Telemetry.Worker.Tests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

public class TelemetryDeadLetterTests : IClassFixture<PostgresFixture>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PostgresFixture _fixture;

    public TelemetryDeadLetterTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Poison telemetry event is dead-lettered after bounded retries")]
    public async Task Poison_telemetry_event_is_dead_lettered_after_bounded_retries()
    {
        await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
        var store = new PostgresTelemetryEventStore(dataSource);
        await store.InitializeAsync();

        var eventId = $"evt_{Guid.NewGuid():N}";
        var json = EnvelopeJson(eventId);
        var envelope = JsonSerializer.Deserialize<TelemetryEventEnvelope>(json, JsonOptions)!;
        await store.InsertAcceptedAsync(envelope, json);

        const int maxAttempts = 3;
        Assert.False(await store.RecordProcessingFailureAsync(eventId, "boom", maxAttempts));
        Assert.False(await store.RecordProcessingFailureAsync(eventId, "boom", maxAttempts));
        Assert.True(await store.RecordProcessingFailureAsync(eventId, "boom", maxAttempts));

        var status = await store.GetStatusAsync(eventId);
        Assert.NotNull(status);
        Assert.Equal("dead_lettered", status.Status);
        Assert.Equal(maxAttempts, status.Attempts);
        Assert.NotNull(status.DeadLetteredAt);

        Assert.False(await store.RecordProcessingFailureAsync(eventId, "boom", maxAttempts));
    }

    private static string EnvelopeJson(string eventId)
    {
        var envelope = new TelemetryEventEnvelope(
            "1.0",
            eventId,
            "urn:metromesh:telemetry:equipment.v1",
            "urn:metromesh:device:gate-1",
            "equipment/gate-1",
            DateTimeOffset.UtcNow,
            "application/json",
            "corr-deadletter-1",
            new TelemetryEventData(
                "eq-1",
                "st-1",
                "door.open",
                DateTimeOffset.UtcNow,
                JsonSerializer.SerializeToElement(new { value = 1 })));

        return JsonSerializer.Serialize(envelope, JsonOptions);
    }
}
