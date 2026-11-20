namespace MetroMesh.Telemetry.Core.Resilience;

public enum CircuitBreakerState
{
    Closed,
    Open,
    HalfOpen
}

public interface ICircuitBreakerPolicy
{
    CircuitBreakerState State { get; }
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, string downstreamService, CancellationToken cancellationToken = default);
    Task ExecuteAsync(Func<Task> action, string downstreamService, CancellationToken cancellationToken = default);
}

public interface IResilientHttpClient
{
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, string downstreamService, CancellationToken cancellationToken = default);
}
