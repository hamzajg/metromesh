using MetroMesh.Analytics.Persistence;
using MetroMesh.Analytics.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MetroMesh.Analytics.Tests;

public class AnalyticsHealthTests
{
    [Fact(DisplayName = "Analytics health endpoints report readiness")]
    public async Task Analytics_health_endpoints_report_readiness()
    {
        var store = new FakeAnalyticsStore();
        var service = new AnalyticsHealthService(
            store,
            new ConfigurationBuilder().Build(),
            NullLogger<AnalyticsHealthService>.Instance);

        var live = await service.HandleRequestAsync("/health/live");
        Assert.Equal(200, live.StatusCode);
        Assert.Contains("live", live.Body);

        var ready = await service.HandleRequestAsync("/health/ready");
        Assert.Equal(200, ready.StatusCode);
        Assert.Contains("ready", ready.Body);

        store.PingOk = false;
        var notReady = await service.HandleRequestAsync("/health/ready");
        Assert.Equal(503, notReady.StatusCode);
        Assert.Contains("not_ready", notReady.Body);

        var missing = await service.HandleRequestAsync("/does-not-exist");
        Assert.Equal(404, missing.StatusCode);
    }
}
