using System.Text.Json;
using Confluent.Kafka;
using MetroMesh.Maintenance.Events;
using MetroMesh.Maintenance.Persistence;
using MetroMesh.Telemetry.Core.Events;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Testcontainers.PostgreSql;
using Testcontainers.Redpanda;
using Xunit;

namespace MetroMesh.Maintenance.Tests;

public sealed class MaintenanceEventTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine").Build();
    private readonly RedpandaContainer _redpanda = new RedpandaBuilder().Build();
    private const string Topic = "maintenance.workorder.v1";

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

    [Fact(DisplayName = "Maintenance mutation emits a domain event to maintenance.workorder.v1")]
    public async Task Maintenance_mutation_emits_a_domain_event_to_maintenance_workorder_v1()
    {
        var store = new PostgresMaintenanceStore(NpgsqlDataSource.Create(_postgres.GetConnectionString()));
        await store.InitializeAsync();

        var request = await store.InsertRequestAsync("eq_turnstile_1", "Gate arm stuck", "high", "manual", null);
        await MaintenanceEvents.EmitAsync(store, "maintenance.request.created", request.Id, new
        {
            request.EquipmentId,
            request.Severity,
            request.Status
        });

        var order = await store.InsertWorkOrderAsync(request.Id, "eq_turnstile_1", "repair", "high", "Fix arm", null);
        await MaintenanceEvents.EmitAsync(store, "maintenance.workorder.created", order.Id, new
        {
            order.RequestId,
            order.EquipmentId,
            order.Type,
            order.Status
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
        Assert.Equal(2, published);

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _redpanda.GetBootstrapAddress(),
            GroupId = $"test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe(Topic);
        var seen = new List<string>();
        for (var i = 0; i < 2; i++)
        {
            var result = consumer.Consume(TimeSpan.FromSeconds(30));
            Assert.NotNull(result);
            seen.Add(result!.Message.Value);
        }
        consumer.Close();

        var types = seen.Select(v => JsonDocument.Parse(v).RootElement.GetProperty("type").GetString()).ToList();
        Assert.Contains("maintenance.request.created", types);
        Assert.Contains("maintenance.workorder.created", types);
        Assert.All(seen, v =>
        {
            using var doc = JsonDocument.Parse(v);
            Assert.Equal("metromesh-maintenance", doc.RootElement.GetProperty("source").GetString());
        });
    }
}
