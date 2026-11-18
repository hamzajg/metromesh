using System.Text.Json;
using MetroMesh.Telemetry.Ingest.Contracts;
using MetroMesh.Telemetry.Ingest.Events;
using MetroMesh.Telemetry.Ingest.Middleware;
using MetroMesh.Telemetry.Ingest.Persistence;
using MetroMesh.Telemetry.Ingest.Validation;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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

await app.Services.GetRequiredService<ITelemetryEventStore>().InitializeAsync();
app.Run();

public partial class Program;
