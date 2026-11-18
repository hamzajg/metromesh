using MetroMesh.Telemetry.Worker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace MetroMesh.Telemetry.Worker.Tests;

public class WorkerHealthTests
{
    [Fact(DisplayName = "Worker health endpoints report readiness")]
    public async Task Worker_health_endpoints_report_readiness()
    {
        var store = new FakeTelemetryEventStore();
        var service = new WorkerHealthService(
            store,
            new ConfigurationBuilder().Build(),
            NullLogger<WorkerHealthService>.Instance);

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
