using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MetroMesh.Equipment.Registry.Persistence;

namespace MetroMesh.Equipment.Registry.Tests;

public class RegistryEndpointTests : IClassFixture<RegistryFactory>
{
    private readonly RegistryFactory _factory;

    public RegistryEndpointTests(RegistryFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Operator registers a station")]
    public async Task Operator_registers_a_station()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-reg-1");

        using var response = await client.PostAsJsonAsync("/v1/stations", new
        {
            name = "Central Station",
            address = "1 Main St",
            status = "open",
            location = "downtown"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal("corr-reg-1", response.Headers.GetValues("X-Correlation-Id").Single());

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        var stationId = root.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(stationId));
        Assert.Equal("Central Station", root.GetProperty("name").GetString());
        Assert.Equal("open", root.GetProperty("status").GetString());

        using var getResponse = await client.GetAsync($"/v1/stations/{stationId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact(DisplayName = "Operator registers equipment against a station")]
    public async Task Operator_registers_equipment_against_a_station()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var stationId = await CreateStationAsync(client);

        using var response = await client.PostAsJsonAsync("/v1/equipment", new
        {
            kind = "turnstile",
            model = "TS-200",
            stationId,
            installDate = "2026-01-15",
            maintenanceThreshold = 500000,
            status = "active"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("turnstile", root.GetProperty("kind").GetString());
        Assert.Equal(stationId, root.GetProperty("stationId").GetString());
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("id").GetString()));
    }

    [Fact(DisplayName = "Invalid registry payload is rejected")]
    public async Task Invalid_registry_payload_is_rejected()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");
        client.DefaultRequestHeaders.Add("X-Correlation-Id", "corr-reg-invalid");

        using var response = await client.PostAsJsonAsync("/v1/equipment", new
        {
            kind = "hoverboard",
            stationId = "st_missing"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.Equal("corr-reg-invalid", root.GetProperty("correlationId").GetString());
        Assert.True(root.GetProperty("errors").TryGetProperty("kind", out _));
        Assert.True(root.GetProperty("errors").TryGetProperty("model", out _));
    }

    [Fact(DisplayName = "Registry request without valid API key is rejected")]
    public async Task Registry_request_without_valid_API_key_is_rejected()
    {
        using var missingKeyClient = _factory.CreateClient();
        using var invalidKeyClient = _factory.CreateClient();
        invalidKeyClient.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "wrong-key");

        using var missingResponse = await missingKeyClient.PostAsJsonAsync("/v1/stations", new { name = "X", address = "Y" });
        using var invalidResponse = await invalidKeyClient.PostAsJsonAsync("/v1/stations", new { name = "X", address = "Y" });

        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
    }

    [Fact(DisplayName = "Station closure cascades to equipment decommission")]
    public async Task Station_closure_cascades_to_equipment_decommission()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var stationId = await CreateStationAsync(client);
        var equipmentId = await CreateEquipmentAsync(client, stationId, "turnstile");

        using var patch = new HttpRequestMessage(HttpMethod.Patch, $"/v1/stations/{stationId}")
        {
            Content = JsonContent.Create(new { status = "closed" })
        };
        using var patchResponse = await client.SendAsync(patch);
        Assert.Equal(HttpStatusCode.OK, patchResponse.StatusCode);

        using var equipmentResponse = await client.GetAsync($"/v1/equipment/{equipmentId}");
        Assert.Equal(HttpStatusCode.OK, equipmentResponse.StatusCode);
        using var doc = JsonDocument.Parse(await equipmentResponse.Content.ReadAsStringAsync());
        Assert.Equal("decommissioned", doc.RootElement.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "Equipment status and station inventory are queryable")]
    public async Task Equipment_status_and_station_inventory_are_queryable()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var stationId = await CreateStationAsync(client);
        var eq1 = await CreateEquipmentAsync(client, stationId, "turnstile");
        var eq2 = await CreateEquipmentAsync(client, stationId, "camera");

        using var inventoryResponse = await client.GetAsync($"/v1/stations/{stationId}/equipment");
        Assert.Equal(HttpStatusCode.OK, inventoryResponse.StatusCode);
        using var inventoryDoc = JsonDocument.Parse(await inventoryResponse.Content.ReadAsStringAsync());
        var items = inventoryDoc.RootElement.EnumerateArray().ToList();
        Assert.Equal(2, items.Count);
        Assert.Contains(items, e => e.GetProperty("id").GetString() == eq1);
        Assert.Contains(items, e => e.GetProperty("id").GetString() == eq2);
        Assert.All(items, e => Assert.Equal("active", e.GetProperty("status").GetString()));

        using var decommission = await client.PostAsync($"/v1/equipment/{eq1}/decommission", null);
        Assert.Equal(HttpStatusCode.OK, decommission.StatusCode);

        using var equipmentResponse = await client.GetAsync($"/v1/equipment/{eq1}");
        using var equipmentDoc = JsonDocument.Parse(await equipmentResponse.Content.ReadAsStringAsync());
        Assert.Equal("decommissioned", equipmentDoc.RootElement.GetProperty("status").GetString());
    }

    [Fact(DisplayName = "Registry mutation emits a domain event")]
    public async Task Registry_mutation_emits_a_domain_event()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var before = RegistryFactory.Store.PendingOutbox.Count;
        var stationId = await CreateStationAsync(client);

        var pending = RegistryFactory.Store.PendingOutbox;
        Assert.True(pending.Count > before, "expected a new outbox event after station registration");
        var evt = pending.Last();
        Assert.Equal("station.created", evt.EventType);
        Assert.Equal(stationId, evt.AggregateId);
        Assert.Contains("station.created", evt.EnvelopeJson);
    }

    [Fact(DisplayName = "Registry service reports health in compose stack")]
    public async Task Registry_service_reports_health_in_compose_stack()
    {
        using var client = _factory.CreateClient();

        using var liveResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        using var readyResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);

        using var doc = JsonDocument.Parse(await readyResponse.Content.ReadAsStringAsync());
        Assert.Equal("ready", doc.RootElement.GetProperty("status").GetString());
    }

    private static async Task<string> CreateEquipmentAsync(HttpClient client, string stationId, string kind)
    {
        using var response = await client.PostAsJsonAsync("/v1/equipment", new
        {
            kind,
            model = $"{kind}-1",
            stationId,
            status = "active"
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    private static async Task<string> CreateStationAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/v1/stations", new
        {
            name = "North Station",
            address = "2 North Ave",
            status = "open"
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }
}

public sealed class RegistryFactory : WebApplicationFactory<Program>
{
    public static readonly FakeRegistryStore Store = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MetroMesh:ApiKey"] = "test-api-key"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRegistryStore>();
            services.AddSingleton<IRegistryStore>(Store);
        });
    }
}
