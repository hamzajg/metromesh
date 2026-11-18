using System.Net;
using System.Text.Json;

namespace MetroMesh.Telemetry.Ingest.Tests;

public class HealthEndpointTests : IClassFixture<MetroMeshFactory>
{
    private readonly MetroMeshFactory _factory;

    public HealthEndpointTests(MetroMeshFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Service health endpoints report readiness")]
    public async Task Service_health_endpoints_report_readiness()
    {
        using var client = _factory.CreateClient();

        using var liveResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        using var readyResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);

        using var doc = JsonDocument.Parse(await readyResponse.Content.ReadAsStringAsync());
        Assert.Equal("ready", doc.RootElement.GetProperty("status").GetString());
    }
}
