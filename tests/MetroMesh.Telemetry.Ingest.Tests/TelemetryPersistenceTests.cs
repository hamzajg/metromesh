using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MetroMesh.Telemetry.Ingest.Events;
using Testcontainers.PostgreSql;
using Testcontainers.Redpanda;

namespace MetroMesh.Telemetry.Ingest.Tests;

public class TelemetryPersistenceTests : IClassFixture<TelemetryInfrastructureFixture>
{
    private readonly TelemetryInfrastructureFixture _infra;

    public TelemetryPersistenceTests(TelemetryInfrastructureFixture infra)
    {
        _infra = infra;
    }

    [Fact(DisplayName = "Accepted telemetry event is persisted and published")]
    public async Task Accepted_telemetry_event_is_persisted_and_published()
    {
        var eventId = $"evt_{Guid.NewGuid():N}";
        using var factory = new IntegrationFactory(_infra);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-persist-1");

        using var post = await client.PostAsync("/v1/events", JsonContent.Create(ValidPayload(eventId)));
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);

        var publisher = factory.Services.GetRequiredService<IOutboxPublisher>();
        var published = await publisher.PublishPendingAsync();
        Assert.True(published >= 1, "expected at least one pending event to publish");

        using var statusResponse = await client.GetAsync($"/v1/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        using var statusDoc = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        var status = statusDoc.RootElement;
        Assert.Equal(eventId, status.GetProperty("eventId").GetString());
        Assert.Equal("published", status.GetProperty("status").GetString());
        Assert.False(status.GetProperty("publishedAt").ValueKind == JsonValueKind.Null);

        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = _infra.KafkaBootstrap,
            GroupId = $"test-{Guid.NewGuid():N}",
            AutoOffsetReset = AutoOffsetReset.Earliest
        }).Build();

        consumer.Subscribe("equipment.telemetry.v1");
        var result = consumer.Consume(TimeSpan.FromSeconds(10));
        Assert.NotNull(result);
        Assert.Equal(eventId, result!.Message.Key);

        using var envelopeDoc = JsonDocument.Parse(result.Message.Value);
        Assert.Equal("corr-persist-1", envelopeDoc.RootElement.GetProperty("correlationId").GetString());
        consumer.Close();
    }

    [Fact(DisplayName = "Telemetry event status can be queried")]
    public async Task Telemetry_event_status_can_be_queried()
    {
        var eventId = $"evt_{Guid.NewGuid():N}";
        using var factory = new IntegrationFactory(_infra);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        using var post = await client.PostAsync("/v1/events", JsonContent.Create(ValidPayload(eventId)));
        Assert.Equal(HttpStatusCode.Accepted, post.StatusCode);

        using var statusResponse = await client.GetAsync($"/v1/events/{eventId}");
        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);

        using var statusDoc = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
        var status = statusDoc.RootElement;
        Assert.Equal(eventId, status.GetProperty("eventId").GetString());
        Assert.Equal("accepted", status.GetProperty("status").GetString());
        Assert.False(status.GetProperty("acceptedAt").ValueKind == JsonValueKind.Null);
        Assert.True(status.GetProperty("publishedAt").ValueKind == JsonValueKind.Null);
    }

    private static object ValidPayload(string eventId) => new
    {
        specversion = "1.0",
        id = eventId,
        type = "equipment.telemetry.turnstile.count",
        source = "gateway-station-01",
        subject = "turnstile/turnstile-001",
        time = "2026-11-18T00:00:00Z",
        datacontenttype = "application/json",
        data = new
        {
            equipmentId = "turnstile-001",
            stationId = "station-001",
            eventType = "turnstile.count.incremented",
            occurredAt = "2026-11-18T00:00:00Z",
            payload = new { count = 1 }
        }
    };

    private sealed class IntegrationFactory : WebApplicationFactory<Program>
    {
        private readonly TelemetryInfrastructureFixture _infra;

        public IntegrationFactory(TelemetryInfrastructureFixture infra)
        {
            _infra = infra;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["MetroMesh:ApiKey"] = "test-api-key",
                    ["ConnectionStrings:TelemetryDb"] = _infra.PostgresConnectionString,
                    ["Kafka:BootstrapServers"] = _infra.KafkaBootstrap,
                    ["Kafka:TelemetryTopic"] = "equipment.telemetry.v1",
                    ["Outbox:Enabled"] = "false"
                });
            });
        }
    }
}

public sealed class TelemetryInfrastructureFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private RedpandaContainer? _redpanda;

    public string PostgresConnectionString { get; private set; } = string.Empty;
    public string KafkaBootstrap { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder("postgres:16-alpine")
            .WithDatabase("metromesh")
            .WithUsername("metromesh")
            .WithPassword("metromesh")
            .Build();
        await _postgres.StartAsync();
        PostgresConnectionString = _postgres.GetConnectionString();

        _redpanda = new RedpandaBuilder("redpandadata/redpanda:v22.2.1").Build();
        await _redpanda.StartAsync();
        KafkaBootstrap = _redpanda.GetBootstrapAddress();
    }

    public async Task DisposeAsync()
    {
        if (_redpanda is not null)
        {
            await _redpanda.DisposeAsync();
        }

        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }
}
