using MetroMesh.Identification.Review.Contracts;
using MetroMesh.Identification.Review.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MetroMesh.Identification.Review.Tests;

public sealed class IdentificationStoreIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact(DisplayName = "Identification store persists interactions idempotently on real Postgres")]
    public async Task Identification_store_persists_interactions_idempotently()
    {
        var dataSource = new NpgsqlDataSourceBuilder(_postgres.GetConnectionString()).Build();
        var store = new IdentificationStore(dataSource);
        await store.InitializeAsync();

        Assert.True(await store.PingAsync());

        var interaction = new CustomerStationInteraction(
            "evt-int-1", "st-1", "ts-1", "identification.passenger.identified", DateTimeOffset.UtcNow, "confirmed", 0.95, "p-1", null, null);

        var first = await store.TryMarkProcessedAsync("evt-int-1");
        var duplicate = await store.TryMarkProcessedAsync("evt-int-1");
        Assert.True(first);
        Assert.False(duplicate);

        await store.RecordInteractionAsync(interaction);
        await store.RecordInteractionAsync(interaction); // redelivery: no duplicate row

        var fetched = await store.GetInteractionAsync("evt-int-1");
        Assert.NotNull(fetched);
        Assert.Equal("st-1", fetched!.StationId);
        Assert.Equal("confirmed", fetched.MatchResult);
    }
}
