using MetroMesh.Telemetry.Ingest.Contracts;
using MetroMesh.Telemetry.Ingest.Middleware;
using MetroMesh.Telemetry.Ingest.Validation;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

app.MapPost("/v1/events", (
        TelemetryEventRequest? request,
        HttpContext httpContext,
        ILogger<Program> logger) =>
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

app.Run();

public partial class Program;
