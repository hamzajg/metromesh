using Microsoft.Extensions.DependencyInjection;

namespace MetroMesh.Telemetry.Core.Observability;

public static class ServiceCatalogExtensions
{
    public static IServiceCollection AddMetroMeshServiceCatalog(this IServiceCollection services)
    {
        services.AddSingleton<IServiceCatalog, ServiceCatalog>();
        return services;
    }
}
