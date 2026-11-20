using MetroMesh.Telemetry.Core.Observability;
using MetroMesh.Telemetry.Core.Resilience;

namespace MetroMesh.Maintenance.Services;

public sealed class CircuitBreakerEquipmentDirectory : IEquipmentDirectory
{
    private readonly IEquipmentDirectory _inner;
    private readonly ICircuitBreakerPolicy _circuitBreaker;
    private readonly ITelemetryMetrics? _metrics;

    public CircuitBreakerEquipmentDirectory(
        IEquipmentDirectory inner,
        ICircuitBreakerPolicy circuitBreaker,
        ITelemetryMetrics? metrics = null)
    {
        _inner = inner;
        _circuitBreaker = circuitBreaker;
        _metrics = metrics;
    }

    public async Task<EquipmentDirectoryEntry?> GetEquipmentAsync(string equipmentId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _circuitBreaker.ExecuteAsync(
                async () => await _inner.GetEquipmentAsync(equipmentId, cancellationToken),
                "registry",
                cancellationToken);
        }
        catch (CircuitBreakerOpenException)
        {
            _metrics?.IncrementCircuitBreakerTrips("registry");
            throw;
        }
    }
}
