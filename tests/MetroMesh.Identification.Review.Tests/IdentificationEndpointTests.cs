using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MetroMesh.Identification.Review.Contracts;
using MetroMesh.Identification.Review.Persistence;
using MetroMesh.Identification.Review.Tests;

namespace MetroMesh.Identification.Review.Tests;

public class IdentificationEndpointTests : IClassFixture<IdentificationFactory>
{
    private readonly IdentificationFactory _factory;

    public IdentificationEndpointTests(IdentificationFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Ingest without a valid API key is rejected")]
    public async Task Ingest_without_valid_api_key_is_rejected()
    {
        using var missingKeyClient = _factory.CreateClient();
        using var invalidKeyClient = _factory.CreateClient();
        invalidKeyClient.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "wrong-key");

        var missingResponse = await missingKeyClient.PostAsync("/v1/identifications", null);
        var invalidResponse = await invalidKeyClient.PostAsync("/v1/identifications", null);

        Assert.Equal(HttpStatusCode.Unauthorized, missingKeyClient.DefaultRequestHeaders.Contains("X-MetroMesh-Api-Key") ? HttpStatusCode.OK : missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
    }

    [Fact(DisplayName = "Ingest endpoint returns accepted for valid identification")]
    public async Task Ingest_endpoint_returns_accepted_for_valid_identification()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var store = _factory.Store;
        await store.RecordInteractionAsync(new CustomerStationInteraction(
            "int-1", "st-1", "ts-1", "identification.passenger.identified", DateTimeOffset.UtcNow, "confirmed", 0.95, "p-1", null, null));

        var request = new
        {
            stationId = "st-1",
            turnstileId = "ts-1",
            eventType = "identification.passenger.identified",
            occurredAt = DateTimeOffset.UtcNow,
            matchResult = "confirmed",
            confidence = 0.95,
            passengerId = "p-1"
        };

        using var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/v1/identifications", content);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact(DisplayName = "Empty station id returns validation error")]
    public async Task Empty_station_id_returns_validation_error()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var request = new
        {
            stationId = "",
            turnstileId = "ts-1",
            eventType = "identification.passenger.identified",
            occurredAt = DateTimeOffset.UtcNow,
            matchResult = "none"
        };

        using var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/v1/identifications", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Health endpoints report status")]
    public async Task Health_endpoints_report_status()
    {
        using var client = _factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}

public sealed class IdentificationFactory : WebApplicationFactory<Program>
{
    private readonly FakeIdentificationStore _store = new();

    public FakeIdentificationStore Store => _store;

    public IdentificationFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__IdentificationDb",
            "Host=localhost;Username=postgres;Password=postgres;Database=postgres");
    }

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
            services.RemoveAll<IIdentificationStore>();
            services.AddSingleton<IIdentificationStore>(_store);
        });
    }
}
