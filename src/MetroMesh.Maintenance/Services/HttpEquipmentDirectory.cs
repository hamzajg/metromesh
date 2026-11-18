using System.Net.Http.Json;
using System.Text.Json;

namespace MetroMesh.Maintenance.Services;

public sealed class HttpEquipmentDirectory : IEquipmentDirectory
{
    private readonly HttpClient _httpClient;

    public HttpEquipmentDirectory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<EquipmentDirectoryEntry?> GetEquipmentAsync(string equipmentId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"/v1/equipment/{Uri.EscapeDataString(equipmentId)}", cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return new EquipmentDirectoryEntry(
            payload.GetProperty("id").GetString() ?? equipmentId,
            payload.GetProperty("kind").GetString() ?? string.Empty,
            payload.GetProperty("status").GetString() ?? string.Empty);
    }
}
