using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Passenger.Loyalty.Persistence;
using MetroMesh.Passenger.Loyalty.Tests;

namespace MetroMesh.Passenger.Loyalty.Tests;

public class PassengerEndpointTests : IClassFixture<PassengerFactory>
{
    private readonly PassengerFactory _factory;

    public PassengerEndpointTests(PassengerFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Create passenger without a valid API key is rejected")]
    public async Task Create_passenger_without_valid_api_key_is_rejected()
    {
        using var missingKeyClient = _factory.CreateClient();
        using var invalidKeyClient = _factory.CreateClient();
        invalidKeyClient.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "wrong-key");

        var missingResponse = await missingKeyClient.PostAsync("/v1/passengers", null);
        var invalidResponse = await invalidKeyClient.PostAsync("/v1/passengers", null);

        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
    }

    [Fact(DisplayName = "Create passenger endpoint returns accepted for valid profile")]
    public async Task Create_passenger_endpoint_returns_accepted_for_valid_profile()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var request = new
        {
            profileReference = "pref-1",
            loyaltyProgramReference = "loyalty-1"
        };

        using var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/v1/passengers", content);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    [Fact(DisplayName = "Empty profile reference returns validation error")]
    public async Task Empty_profile_reference_returns_validation_error()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var request = new { profileReference = "", loyaltyProgramReference = "loyalty-1" };

        using var content = new StringContent(JsonSerializer.Serialize(request), System.Text.Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/v1/passengers", content);
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

public sealed class PassengerFactory : WebApplicationFactory<Program>
{
    private readonly FakePassengerStore _store = new();

    public FakePassengerStore Store => _store;

    public PassengerFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__PassengerDb",
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
            services.RemoveAll<IPassengerStore>();
            services.AddSingleton<IPassengerStore>(_store);
        });
    }
}
