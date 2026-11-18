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
            services.AddSingleton<IRegistryStore>(new FakeRegistryStore());
        });
    }
}
