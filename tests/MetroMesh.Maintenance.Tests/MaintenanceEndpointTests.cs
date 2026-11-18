using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MetroMesh.Maintenance.Persistence;
using MetroMesh.Maintenance.Services;
using Xunit;

namespace MetroMesh.Maintenance.Tests;

public sealed class MaintenanceEndpointTests : IClassFixture<MaintenanceFactory>
{
    private readonly MaintenanceFactory _factory;

    public MaintenanceEndpointTests(MaintenanceFactory factory)
    {
        _factory = factory;
        MaintenanceFactory.Directory.Add("eq_turnstile_1");
        MaintenanceFactory.Directory.Add("eq_camera_1", "camera");
    }

    [Fact(DisplayName = "Planner raises a maintenance request")]
    public async Task Planner_raises_a_maintenance_request()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var response = await client.PostAsJsonAsync("/v1/maintenance-requests", new
        {
            equipmentId = "eq_turnstile_1",
            reason = "Turnstile gate arm stuck",
            severity = "high",
            source = "manual"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.StartsWith("mr_", root.GetProperty("id").GetString());
        Assert.Equal("eq_turnstile_1", root.GetProperty("equipmentId").GetString());
        Assert.Equal("open", root.GetProperty("status").GetString());

        using var getResponse = await client.GetAsync($"/v1/maintenance-requests/{root.GetProperty("id").GetString()}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact(DisplayName = "Planner creates a work order for equipment")]
    public async Task Planner_creates_a_work_order_for_equipment()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var response = await client.PostAsJsonAsync("/v1/work-orders", new
        {
            equipmentId = "eq_camera_1",
            type = "inspection",
            priority = "medium",
            title = "Quarterly camera lens check"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;
        Assert.StartsWith("wo_", root.GetProperty("id").GetString());
        Assert.Equal("open", root.GetProperty("status").GetString());
        Assert.Equal("eq_camera_1", root.GetProperty("equipmentId").GetString());
    }

    [Fact(DisplayName = "Invalid maintenance payload is rejected")]
    public async Task Invalid_maintenance_payload_is_rejected()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var response = await client.PostAsJsonAsync("/v1/work-orders", new
        {
            equipmentId = "eq_turnstile_1",
            type = "not-a-type",
            priority = "medium"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(doc.RootElement.TryGetProperty("correlationId", out _));
    }

    [Fact(DisplayName = "Maintenance request without valid API key is rejected")]
    public async Task Maintenance_request_without_valid_API_key_is_rejected()
    {
        using var client = _factory.CreateClient();

        var noKey = await client.PostAsJsonAsync("/v1/maintenance-requests", new
        {
            equipmentId = "eq_turnstile_1",
            reason = "test",
            severity = "low"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, noKey.StatusCode);

        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "wrong-key");
        var wrongKey = await client.PostAsJsonAsync("/v1/work-orders", new
        {
            equipmentId = "eq_turnstile_1",
            type = "repair",
            priority = "low",
            title = "test"
        });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongKey.StatusCode);
    }

    [Fact(DisplayName = "Work order can link to a maintenance request")]
    public async Task Work_order_can_link_to_a_maintenance_request()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var requestResponse = await client.PostAsJsonAsync("/v1/maintenance-requests", new
        {
            equipmentId = "eq_turnstile_1",
            reason = "Gate motor overheating",
            severity = "critical",
            source = "signal"
        });
        Assert.Equal(HttpStatusCode.Created, requestResponse.StatusCode);
        using var requestDoc = JsonDocument.Parse(await requestResponse.Content.ReadAsStringAsync());
        var requestId = requestDoc.RootElement.GetProperty("id").GetString();

        var orderResponse = await client.PostAsJsonAsync("/v1/work-orders", new
        {
            equipmentId = "eq_turnstile_1",
            requestId,
            type = "repair",
            priority = "urgent",
            title = "Replace gate motor"
        });

        Assert.Equal(HttpStatusCode.Created, orderResponse.StatusCode);
        using var orderDoc = JsonDocument.Parse(await orderResponse.Content.ReadAsStringAsync());
        Assert.Equal(requestId, orderDoc.RootElement.GetProperty("requestId").GetString());
    }

    [Fact(DisplayName = "Work against unknown equipment is rejected")]
    public async Task Work_against_unknown_equipment_is_rejected()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var response = await client.PostAsJsonAsync("/v1/work-orders", new
        {
            equipmentId = "eq_missing",
            type = "repair",
            priority = "low",
            title = "test"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Work against decommissioned equipment is rejected")]
    public async Task Work_against_decommissioned_equipment_is_rejected()
    {
        MaintenanceFactory.Directory.Add("eq_old_cam", "camera", "decommissioned");
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var response = await client.PostAsJsonAsync("/v1/maintenance-requests", new
        {
            equipmentId = "eq_old_cam",
            reason = "test",
            severity = "low"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

public sealed class MaintenanceFactory : WebApplicationFactory<Program>
{
    public static readonly FakeMaintenanceStore Store = new();
    public static readonly FakeEquipmentDirectory Directory = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MetroMesh:ApiKey"] = "test-api-key",
                ["ConnectionStrings:MaintenanceDb"] = "Host=localhost;Database=unused"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMaintenanceStore>();
            services.AddSingleton<IMaintenanceStore>(Store);
            services.RemoveAll<IEquipmentDirectory>();
            services.AddSingleton<IEquipmentDirectory>(Directory);
        });
    }
}
