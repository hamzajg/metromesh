using MetroMesh.Equipment.Registry.Persistence;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MetroMesh.Equipment.Registry.Tests;

public sealed class RegistryPostgresFixture : IAsyncLifetime
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

public class RegistryPersistenceTests : IClassFixture<RegistryPostgresFixture>
{
    private readonly RegistryPostgresFixture _fixture;

    public RegistryPersistenceTests(RegistryPostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Operator registers a station")]
    public async Task Operator_registers_a_station_persists()
    {
        await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
        var store = new PostgresRegistryStore(dataSource);
        await store.InitializeAsync();

        var station = await store.InsertStationAsync("Central", "1 Main St", "open", "downtown");
        Assert.StartsWith("st_", station.Id);

        var fetched = await store.GetStationAsync(station.Id);
        Assert.NotNull(fetched);
        Assert.Equal("Central", fetched.Name);
        Assert.Equal("open", fetched.Status);
    }

    [Fact(DisplayName = "Operator registers equipment against a station")]
    public async Task Operator_registers_equipment_persists()
    {
        await using var dataSource = NpgsqlDataSource.Create(_fixture.ConnectionString);
        var store = new PostgresRegistryStore(dataSource);
        await store.InitializeAsync();

        var station = await store.InsertStationAsync("North", "2 North Ave", "open", null);
        var equipment = await store.InsertEquipmentAsync(
            "camera",
            "CAM-900",
            station.Id,
            new DateOnly(2026, 1, 10),
            1000,
            "active");

        Assert.NotNull(equipment);
        Assert.StartsWith("eq_", equipment!.Id);
        Assert.Equal(station.Id, equipment.StationId);

        var missing = await store.InsertEquipmentAsync("turnstile", "TS-1", "st_missing", null, null, "active");
        Assert.Null(missing);
    }
}
