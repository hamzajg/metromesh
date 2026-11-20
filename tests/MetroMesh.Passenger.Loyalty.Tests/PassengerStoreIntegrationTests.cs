using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Passenger.Loyalty.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MetroMesh.Passenger.Loyalty.Tests;

public sealed class PassengerStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact(DisplayName = "Passenger store persists profiles idempotently on real Postgres")]
    public async Task Passenger_store_persists_profiles_idempotently()
    {
        var dataSource = new NpgsqlDataSourceBuilder(_postgres.GetConnectionString()).Build();
        var store = new PassengerStore(dataSource);
        await store.InitializeAsync();

        Assert.True(await store.PingAsync());

        var profile = new PassengerProfile(
            "evt-int-1", "pref-int-1", "loyalty-int-1", "active", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

        var first = await store.TryMarkProcessedAsync("evt-int-1");
        var duplicate = await store.TryMarkProcessedAsync("evt-int-1");
        Assert.True(first);
        Assert.False(duplicate);

        await store.RecordPassengerAsync(profile);
        await store.RecordPassengerAsync(profile); // redelivery: no duplicate row

        var fetched = await store.GetPassengerAsync("evt-int-1");
        Assert.NotNull(fetched);
        Assert.Equal("pref-int-1", fetched!.ProfileReference);
    }
}
