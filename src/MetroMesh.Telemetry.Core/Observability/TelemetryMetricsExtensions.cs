using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MetroMesh.Telemetry.Core.Observability;

public static class TelemetryExtensions
{
    public static IServiceCollection AddMetroMeshMetrics(this IServiceCollection services, string serviceName)
    {
        services.AddSingleton<ITelemetryMetrics, TelemetryMetrics>();

        services.AddOpenTelemetry()
            .WithMetrics(builder =>
            {
                builder.AddMeter("MetroMesh");
            });

        services.AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder
                    .AddSource("MetroMesh")
                    .AddConsoleExporter();
            });

        return services;
    }
}
