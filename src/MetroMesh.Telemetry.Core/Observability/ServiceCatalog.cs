namespace MetroMesh.Telemetry.Core.Observability;

public interface IServiceCatalog
{
    void RegisterService(string name, string version, string endpoint, IDictionary<string, string> metadata);
    void UpdateHealth(string name, bool healthy);
    IReadOnlyList<ServiceEntry> ListServices();
}

public sealed record ServiceEntry(
    string Name,
    string Version,
    string Endpoint,
    bool Healthy,
    IDictionary<string, string> Metadata);

public sealed class ServiceCatalog : IServiceCatalog
{
    private readonly Dictionary<string, ServiceEntry> _services = new();

    public void RegisterService(string name, string version, string endpoint, IDictionary<string, string> metadata)
    {
        _services[name] = new ServiceEntry(name, version, endpoint, true, metadata);
    }

    public void UpdateHealth(string name, bool healthy)
    {
        if (_services.TryGetValue(name, out var entry))
        {
            _services[name] = entry with { Healthy = healthy };
        }
    }

    public IReadOnlyList<ServiceEntry> ListServices() => _services.Values.ToList();
}
