using System.Net.Http.Json;
using MetroMesh.Telemetry.Core.Observability;

namespace MetroMesh.Telemetry.Core.Resilience;

public sealed class ConsulServiceRegistration : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _serviceId;
    private readonly string _consulUrl;
    private readonly CancellationTokenSource _cts = new();
    private bool _disposed;

    public ConsulServiceRegistration(
        string serviceId,
        string serviceName,
        string address,
        int port,
        string consulUrl,
        IDictionary<string, string>? tags = null,
        IDictionary<string, string>? checks = null)
    {
        _serviceId = serviceId;
        _consulUrl = consulUrl.TrimEnd('/');
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };

        var registration = new
        {
            id = serviceId,
            name = serviceName,
            address = address,
            port = port,
            tags = tags?.Keys.ToArray() ?? Array.Empty<string>(),
            check = checks
        };

        try
        {
            _httpClient.PutAsJsonAsync($"{_consulUrl}/v1/agent/service/register", registration, _cts.Token)
                .GetAwaiter().GetResult();
        }
        catch
        {
            // Consul may not be available in all environments; registration is best-effort
        }
    }

    public void Deregister()
    {
        try
        {
            _httpClient.PutAsync($"{_consulUrl}/v1/agent/service/deregister/{_serviceId}", null, _cts.Token)
                .GetAwaiter().GetResult();
        }
        catch
        {
            // Best-effort deregistration
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            Deregister();
            _httpClient.Dispose();
            _cts.Dispose();
            _disposed = true;
        }
    }
}
