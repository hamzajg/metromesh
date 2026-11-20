using System.Diagnostics;
using System.Text.Json;
using MetroMesh.Telemetry.Core.Contracts;
using MetroMesh.Telemetry.Core.Events;
using MetroMesh.Telemetry.Core.Observability;
using MetroMesh.Telemetry.Core.Persistence;
using MetroMesh.Telemetry.Ingest.Events;
using MetroMesh.Telemetry.Ingest.Middleware;
using MetroMesh.Telemetry.Ingest.Validation;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "MetroMesh Ingest API", Version = "1.0.0" });
});

builder.Services.AddMetroMeshMetrics("ingest");
builder.Services.AddMetroMeshServiceCatalog();

builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("TelemetryDb")
        ?? builder.Configuration["METROMESH_DB"];

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:TelemetryDb or METROMESH_DB is required.");
    }

    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddSingleton<ITelemetryEventStore, PostgresTelemetryEventStore>();
builder.Services.AddSingleton<IEventBus, ConfluentKafkaEventBus>();
builder.Services.AddSingleton<IOutboxPublisher, KafkaOutboxPublisher>();
builder.Services.AddHostedService<OutboxPublisherService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapPost("/v1/events", async (
        TelemetryEventRequest? request,
        HttpContext httpContext,
        ITelemetryEventStore store,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        var errors = TelemetryEventValidator.Validate(request);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                detail: "The telemetry event payload is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid telemetry event",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        var accepted = request!;
        var envelope = new TelemetryEventEnvelope(
            accepted.SpecVersion!,
            accepted.Id!,
            accepted.Type!,
            accepted.Source!,
            accepted.Subject!,
            accepted.Time!.Value,
            accepted.DataContentType!,
            correlationId,
            Activity.Current?.Id,
            accepted.Data!);
        var envelopeJson = JsonSerializer.Serialize(envelope, jsonOptions);

        await store.InsertAcceptedAsync(envelope, envelopeJson, cancellationToken);

        var response = new AcceptedEventResponse(
            accepted.Id!,
            "accepted",
            correlationId,
            DateTimeOffset.UtcNow);

        logger.LogInformation(
            "Telemetry event {EventId} accepted for type {EventType} with correlation {CorrelationId}",
            accepted.Id,
            accepted.Type,
            correlationId);

        return Results.Accepted($"/v1/events/{accepted.Id}", response);
    })
    .WithName("SubmitTelemetryEvent")
    .WithOpenApi();

app.MapGet("/v1/events/{eventId}", async (
        string eventId,
        ITelemetryEventStore store,
        CancellationToken cancellationToken) =>
    {
        var status = await store.GetStatusAsync(eventId, cancellationToken);
        return status is null ? Results.NotFound() : Results.Ok(status);
    })
    .WithName("GetTelemetryEventStatus")
    .WithOpenApi();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .WithName("Liveness")
    .WithOpenApi();

app.MapGet("/health/ready", async (ITelemetryEventStore store, CancellationToken cancellationToken) =>
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

app.MapGet("/metrics", (ITelemetryMetrics metrics) =>
    Results.Text(metrics.ToPrometheusFormat(), "text/plain; version=0.0.4; charset=utf-8"))
    .WithName("Metrics")
    .WithOpenApi();

app.MapGet("/catalog", (IServiceCatalog catalog) =>
    Results.Ok(catalog.ListServices()))
    .WithName("ServiceCatalog")
    .WithOpenApi();

var catalogIngest = app.Services.GetRequiredService<IServiceCatalog>();
catalogIngest.RegisterService("ingest", "1.0.0", "http://localhost:8080", new Dictionary<string, string>
{
    ["protocol"] = "http",
    ["port"] = "8080"
});

await app.Services.GetRequiredService<ITelemetryEventStore>().InitializeAsync();
app.Run();

public partial class Program;
