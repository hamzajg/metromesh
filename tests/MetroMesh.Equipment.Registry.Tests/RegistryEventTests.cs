using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using MetroMesh.Equipment.Registry.Events;
using MetroMesh.Equipment.Registry.Persistence;
using MetroMesh.Telemetry.Core.Events;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Testcontainers.Redpanda;
using Xunit;

namespace MetroMesh.Equipment.Registry.Tests;

public sealed class RegistryEventTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly RedpandaContainer _redpanda = new RedpandaBuilder().Build();
    private const string Topic = "equipment.registry.v1";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _redpanda.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _redpanda.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    [Fact(DisplayName = "Registry mutation emits a domain event to equipment.registry.v1")]
    public async Task Registry_mutation_emits_a_domain_event_to_equipment_registry_v1()
    {
        var store = new PostgresRegistryStore(new Npgsql.NpgsqlDataSourceBuilder(_postgres.GetConnectionString()).Build());
        await store.InitializeAsync();

        var station = await store.InsertStationAsync("Central Station", "1 Main St", "open", null);

        await RegistryEvents.EmitAsync(store, "station.created", station.Id, new
        {
            station.Name,
            station.Status
        });

        var publisher = new KafkaOutboxPublisher(
            store,
            new ConfluentKafkaEventBus(new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Kafka:BootstrapServers"] = _redpanda.GetBootstrapAddress()
                })
                .Build()),
            new ConfigurationBuilder().Build(),
            NullLogger<KafkaOutboxPublisher>.Instance);

        var published = await publisher.PublishPendingAsync();
        Assert.Equal(1, published);

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _redpanda.GetBootstrapAddress(),
            GroupId = $"test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe(Topic);
        var result = consumer.Consume(TimeSpan.FromSeconds(30));
        Assert.NotNull(result);
        Assert.Equal(station.Id, result!.Message.Key);

        using var doc = JsonDocument.Parse(result.Message.Value);
        Assert.Equal("station.created", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(station.Id, doc.RootElement.GetProperty("subject").GetString());
        Assert.Equal("metromesh-registry", doc.RootElement.GetProperty("source").GetString());
        Assert.Equal("Central Station", doc.RootElement.GetProperty("data").GetProperty("name").GetString());
        consumer.Close();
    }
}
