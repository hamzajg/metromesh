using System.Text.Json;
using MetroMesh.Analytics.Contracts;
using MetroMesh.Analytics.Middleware;
using MetroMesh.Analytics.Persistence;
using MetroMesh.Analytics.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("AnalyticsDb")
    ?? builder.Configuration["METROMESH_DB"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:AnalyticsDb or METROMESH_DB is required.");
}

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IAnalyticsStore, AnalyticsStore>();
builder.Services.AddSingleton<AnalyticsEventProcessor>();
builder.Services.AddHostedService<AnalyticsConsumerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapGet("/v1/ridership/counts", async (
        string? stationId,
        string? equipmentId,
        string? from,
        string? to,
        IAnalyticsStore store,
        CancellationToken cancellationToken) =>
    {
        var fromTime = string.IsNullOrWhiteSpace(from) ? null : (DateTimeOffset?)DateTimeOffset.Parse(from);
        var toTime = string.IsNullOrWhiteSpace(to) ? null : (DateTimeOffset?)DateTimeOffset.Parse(to);

        var counts = await store.GetCountsAsync(stationId, equipmentId, fromTime, toTime, cancellationToken);
        return Results.Ok(counts);
    })
    .WithName("GetRidershipCounts")
    .WithOpenApi();

app.MapGet("/v1/ridership/series", async (
        string? stationId,
        string? equipmentId,
        string bucket,
        string? from,
        string? to,
        IAnalyticsStore store,
        CancellationToken cancellationToken) =>
    {
        var fromTime = string.IsNullOrWhiteSpace(from) ? null : (DateTimeOffset?)DateTimeOffset.Parse(from);
        var toTime = string.IsNullOrWhiteSpace(to) ? null : (DateTimeOffset?)DateTimeOffset.Parse(to);

        var series = await store.GetSeriesAsync(stationId, equipmentId, bucket, fromTime, toTime, cancellationToken);
        return Results.Ok(series);
    })
    .WithName("GetRidershipSeries")
    .WithOpenApi();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .WithName("Liveness")
    .WithOpenApi();

app.MapGet("/health/ready", async (IAnalyticsStore store, CancellationToken cancellationToken) =>
    {
        try
        {
            var ready = await store.PingAsync(cancellationToken);
            return ready
                ? Results.Ok(new { status = "ready" })
                : Results.Json(new { status = "not_ready" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch
        {
            return Results.Json(new { status = "not_ready" }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    })
    .WithName("Readiness")
    .WithOpenApi();

await app.Services.GetRequiredService<IAnalyticsStore>().InitializeAsync();
app.Run();

public partial class Program;
