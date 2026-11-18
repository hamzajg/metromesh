using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MetroMesh.Telemetry.Ingest.Events;
using MetroMesh.Telemetry.Core.Persistence;

namespace MetroMesh.Telemetry.Ingest.Tests;

public class TelemetryEndpointTests : IClassFixture<MetroMeshFactory>
{
    private readonly MetroMeshFactory _factory;

    public TelemetryEndpointTests(MetroMeshFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Gateway submits valid telemetry event")]
    public async Task Gateway_submits_valid_telemetry_event()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-valid-1");

        using var response = await client.PostAsync("/v1/events", JsonContent.Create(ValidPayload));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal("corr-valid-1", response.Headers.GetValues("X-Correlation-Id").Single());

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("eventId").GetString()));
        Assert.Equal("accepted", root.GetProperty("status").GetString());
        Assert.Equal("corr-valid-1", root.GetProperty("correlationId").GetString());
    }

    [Fact(DisplayName = "Telemetry payload with missing required fields is rejected")]
    public async Task Telemetry_payload_with_missing_required_fields_is_rejected()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-invalid-1");

        using var response = await client.PostAsync("/v1/events", JsonContent.Create(MissingSubjectPayload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("problem+json", response.Content.Headers.ContentType?.MediaType);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("corr-invalid-1", root.GetProperty("correlationId").GetString());
        Assert.Contains("subject", root.GetProperty("errors").GetProperty("subject")[0].GetString());
    }

    [Fact(DisplayName = "Telemetry request without valid API key is rejected")]
    public async Task Telemetry_request_without_valid_API_key_is_rejected()
    {
        using var missingKeyClient = _factory.CreateClient();
        using var invalidKeyClient = _factory.CreateClient();
        invalidKeyClient.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "wrong-key");

        using var missingResponse = await missingKeyClient.PostAsync("/v1/events", JsonContent.Create(ValidPayload));
        using var invalidResponse = await invalidKeyClient.PostAsync("/v1/events", JsonContent.Create(ValidPayload));

        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
    }

    private static readonly object ValidPayload = new
    {
        specversion = "1.0",
        id = "evt_123",
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

    private static readonly object MissingSubjectPayload = new
    {
        specversion = "1.0",
        id = "evt_124",
        type = "equipment.telemetry.turnstile.count",
        source = "gateway-station-01",
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

}

public sealed class MetroMeshFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MetroMesh:ApiKey"] = "test-api-key",
                ["Outbox:Enabled"] = "false"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ITelemetryEventStore>();
            services.RemoveAll<IEventBus>();
            services.AddSingleton<ITelemetryEventStore>(new FakeTelemetryEventStore());
            services.AddSingleton<IEventBus>(new FakeEventBus());
        });
    }
}
