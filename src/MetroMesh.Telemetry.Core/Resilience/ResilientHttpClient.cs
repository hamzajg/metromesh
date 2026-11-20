using System.Net;
using System.Text;

using MetroMesh.Telemetry.Core.Observability;

namespace MetroMesh.Telemetry.Core.Resilience;

public sealed class ResilientHttpClient : IResilientHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ICircuitBreakerPolicy _circuitBreaker;
    private readonly ITelemetryMetrics? _metrics;
    private readonly string _serviceName;

    public ResilientHttpClient(
        HttpClient httpClient,
        ICircuitBreakerPolicy circuitBreaker,
        string serviceName,
        ITelemetryMetrics? metrics = null)
    {
        _httpClient = httpClient;
        _circuitBreaker = circuitBreaker;
        _serviceName = serviceName;
        _metrics = metrics;
    }

    public async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        string downstreamService,
        CancellationToken cancellationToken = default)
    {
        var start = DateTimeOffset.UtcNow;

        try
        {
            var response = await _circuitBreaker.ExecuteAsync(
                async () => await _httpClient.SendAsync(request, cancellationToken),
                downstreamService,
                cancellationToken);

            var duration = (DateTimeOffset.UtcNow - start).TotalMilliseconds;
            _metrics?.RecordRequestDuration(_serviceName, request.RequestUri?.PathAndQuery ?? "unknown", duration);
            _metrics?.IncrementRequestsReceived(_serviceName, request.RequestUri?.PathAndQuery ?? "unknown");

            return response;
        }
        catch (CircuitBreakerOpenException)
        {
            _metrics?.IncrementCircuitBreakerTrips(downstreamService);
            var fallback = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent($"Downstream service {downstreamService} is unavailable (circuit breaker open)", Encoding.UTF8, "text/plain")
            };
            return fallback;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            _metrics?.IncrementCircuitBreakerTrips(downstreamService);
            var fallback = new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent($"Downstream service {downstreamService} is unreachable", Encoding.UTF8, "text/plain")
            };
            return fallback;
        }
    }
}
