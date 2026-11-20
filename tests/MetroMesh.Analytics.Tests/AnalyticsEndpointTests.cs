using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MetroMesh.Analytics.Contracts;
using MetroMesh.Analytics.Persistence;
using MetroMesh.Analytics.Tests;

namespace MetroMesh.Analytics.Tests;

public class AnalyticsEndpointTests : IClassFixture<AnalyticsFactory>
{
    private readonly AnalyticsFactory _factory;

    public AnalyticsEndpointTests(AnalyticsFactory factory)
    {
        _factory = factory;
    }

    [Fact(DisplayName = "Counts without a valid API key are rejected")]
    public async Task Counts_without_valid_api_key_are_rejected()
    {
        using var missingKeyClient = _factory.CreateClient();
        using var invalidKeyClient = _factory.CreateClient();
        invalidKeyClient.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "wrong-key");

        var missingResponse = await missingKeyClient.GetAsync("/v1/ridership/counts");
        var invalidResponse = await invalidKeyClient.GetAsync("/v1/ridership/counts");

        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
    }

    [Fact(DisplayName = "Counts endpoint returns readings grouped by station and equipment")]
    public async Task Counts_endpoint_returns_readings_grouped_by_station_and_equipment()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var store = _factory.Store;
        await store.RecordReadingAsync(new RidershipReading("evt-1", "st-1", "eq-1", "turnstile.count.incremented", DateTimeOffset.UtcNow, 3));
        await store.RecordReadingAsync(new RidershipReading("evt-2", "st-1", "eq-1", "turnstile.count.incremented", DateTimeOffset.UtcNow, 2));
        await store.RecordReadingAsync(new RidershipReading("evt-3", "st-2", "eq-2", "turnstile.count.incremented", DateTimeOffset.UtcNow, 1));

        using var response = await client.GetAsync("/v1/ridership/counts?stationId=st-1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var counts = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(counts);
        Assert.Equal("st-1", counts[0].GetProperty("stationId").GetString());
        Assert.Equal("eq-1", counts[0].GetProperty("equipmentId").GetString());
        Assert.Equal(5, counts[0].GetProperty("count").GetInt32());
    }

    [Fact(DisplayName = "Empty window returns zero counts, not an error")]
    public async Task Empty_window_returns_zero_counts_not_an_error()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var farFuture = "9999-01-01T00:00:00Z";
        using var response = await client.GetAsync($"/v1/ridership/counts?from={farFuture}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(doc.RootElement.EnumerateArray());
    }

    [Fact(DisplayName = "Series endpoint returns per-bucket counts")]
    public async Task Series_endpoint_returns_per_bucket_counts()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", "test-api-key");

        var store = _factory.Store;
        var now = DateTimeOffset.UtcNow;
        var hourStart = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, 0, 0, TimeSpan.Zero);
        await store.RecordReadingAsync(new RidershipReading("evt-1", "st-1", "eq-1", "turnstile.count.incremented", hourStart, 2));
        await store.RecordReadingAsync(new RidershipReading("evt-2", "st-1", "eq-1", "turnstile.count.incremented", hourStart.AddMinutes(20), 3));

        using var response = await client.GetAsync($"/v1/ridership/series?stationId=st-1&bucket=hour&from={Uri.EscapeDataString(hourStart.AddHours(-1).ToString("o"))}&to={Uri.EscapeDataString(hourStart.AddHours(2).ToString("o"))}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var buckets = doc.RootElement.EnumerateArray().ToList();
        Assert.Single(buckets);
        Assert.Equal(5, buckets[0].GetProperty("count").GetInt32());
    }

    [Fact(DisplayName = "Health endpoints report status")]
    public async Task Health_endpoints_report_status()
    {
        using var client = _factory.CreateClient();

        var live = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, live.StatusCode);

        var ready = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }
}

public sealed class AnalyticsFactory : WebApplicationFactory<Program>
{
    private readonly FakeAnalyticsStore _store = new();

    public FakeAnalyticsStore Store => _store;

    public AnalyticsFactory()
    {
        Environment.SetEnvironmentVariable(
            "ConnectionStrings__AnalyticsDb",
            "Host=localhost;Username=postgres;Password=postgres;Database=postgres");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MetroMesh:ApiKey"] = "test-api-key"
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IAnalyticsStore>();
            services.AddSingleton<IAnalyticsStore>(_store);
        });
    }
}
