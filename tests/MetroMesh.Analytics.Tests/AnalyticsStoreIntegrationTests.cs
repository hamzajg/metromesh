using MetroMesh.Analytics.Contracts;
using MetroMesh.Analytics.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MetroMesh.Analytics.Tests;

public sealed class AnalyticsStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact(DisplayName = "Analytics store persists readings idempotently on real Postgres")]
    public async Task Analytics_store_persists_readings_idempotently()
    {
        var dataSource = new NpgsqlDataSourceBuilder(_postgres.GetConnectionString()).Build();
        var store = new AnalyticsStore(dataSource);
        await store.InitializeAsync();

        Assert.True(await store.PingAsync());

        var reading = new RidershipReading("evt-int-1", "st-1", "eq-1", "turnstile.count.incremented", DateTimeOffset.UtcNow, 2);

        var first = await store.TryMarkProcessedAsync("evt-int-1");
        var duplicate = await store.TryMarkProcessedAsync("evt-int-1");
        Assert.True(first);
        Assert.False(duplicate);

        await store.RecordReadingAsync(reading);
        await store.RecordReadingAsync(reading); // redelivery: no duplicate row

        var deadLettered = await store.RecordProcessingFailureAsync("evt-int-1", "boom", 3);
        Assert.False(deadLettered);
    }
}
