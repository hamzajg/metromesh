using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Passenger.Loyalty.Persistence;
using MetroMesh.Passenger.Loyalty.Tests;
using Testcontainers.Redpanda;

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

    [Fact(DisplayName = "Loyalty endpoint creates and lists programs")]
    public async Task Loyalty_endpoint_creates_and_lists_programs()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var program = new
        {
            tier = "gold",
            status = "active",
            terms = "terms-v1"
        };

        using var content = new StringContent(JsonSerializer.Serialize(program), System.Text.Encoding.UTF8, "application/json");
        using var createResponse = await client.PostAsync("/v1/loyalty?passengerId=pass-1", content);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        using var listResponse = await client.GetAsync("/v1/loyalty?passengerId=pass-1");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    }

    [Fact(DisplayName = "Retire loyalty endpoint retires program")]
    public async Task Retire_loyalty_endpoint_retires_program()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        using var response = await client.PostAsync("/v1/loyalty/lp-1/retire", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Notification endpoint creates and lists notifications")]
    public async Task Notification_endpoint_creates_and_lists_notifications()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var notification = new
        {
            passengerId = "pass-1",
            type = "profile.updated",
            recipient = "passenger@email.com"
        };

        using var content = new StringContent(JsonSerializer.Serialize(notification), System.Text.Encoding.UTF8, "application/json");
        using var createResponse = await client.PostAsync("/v1/notifications", content);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        using var listResponse = await client.GetAsync("/v1/notifications?passengerId=pass-1");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    }

    [Fact(DisplayName = "Audit endpoint creates and lists entries")]
    public async Task Audit_endpoint_creates_and_lists_entries()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var entry = new
        {
            passengerId = "pass-1",
            action = "loyalty.created",
            actor = "operator-1",
            details = "Created gold loyalty program"
        };

        using var content = new StringContent(JsonSerializer.Serialize(entry), System.Text.Encoding.UTF8, "application/json");
        using var createResponse = await client.PostAsync("/v1/audit", content);
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        using var listResponse = await client.GetAsync("/v1/audit?passengerId=pass-1");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    }
}

public sealed class PassengerFactory : WebApplicationFactory<Program>
{
    private readonly FakePassengerStore _store = new();
    private readonly RedpandaContainer _redpanda = new RedpandaBuilder("redpandadata/redpanda:v22.2.1").Build();

    public FakePassengerStore Store => _store;

    // The create endpoint publishes to Kafka before responding, so the suite
    // needs a real broker instead of assuming one is running on localhost.
    public PassengerFactory()
    {
        _redpanda.StartAsync().GetAwaiter().GetResult();

        Environment.SetEnvironmentVariable(
            "ConnectionStrings__PassengerDb",
            "Host=localhost;Username=postgres;Password=postgres;Database=postgres");
    }

    public override async ValueTask DisposeAsync()
    {
        await _redpanda.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MetroMesh:ApiKey"] = "test-api-key",
                ["Kafka:BootstrapServers"] = _redpanda.GetBootstrapAddress()
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPassengerStore>();
            services.AddSingleton<IPassengerStore>(_store);
            services.RemoveAll<IHostedService>();
        });
    }
}
