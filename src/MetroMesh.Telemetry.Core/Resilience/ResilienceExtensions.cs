using MetroMesh.Telemetry.Core.Observability;
using Microsoft.Extensions.DependencyInjection;

namespace MetroMesh.Telemetry.Core.Resilience;

public static class ResilienceExtensions
{
    public static IServiceCollection AddMetroMeshResilience(
        this IServiceCollection services,
        string serviceName,
        int failureThreshold = 5,
        TimeSpan? resetTimeout = null)
    {
        services.AddSingleton<ICircuitBreakerPolicy>(sp =>
            new CircuitBreakerPolicy(failureThreshold, resetTimeout));

        services.AddHttpClient<IResilientHttpClient, ResilientHttpClient>((sp, client) =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5)
        });

        services.AddTransient(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient();
            var circuitBreaker = sp.GetRequiredService<ICircuitBreakerPolicy>();
            var metrics = sp.GetService<ITelemetryMetrics>();
            return new ResilientHttpClient(httpClient, circuitBreaker, serviceName, metrics);
        });

        return services;
    }
}
