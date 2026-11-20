using System.Text.Json;
using MetroMesh.Identification.Review.Contracts;
using MetroMesh.Identification.Review.Middleware;
using MetroMesh.Identification.Review.Persistence;
using MetroMesh.Identification.Review.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var connectionString = builder.Configuration.GetConnectionString("IdentificationDb")
    ?? builder.Configuration["METROMESH_DB"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:IdentificationDb or METROMESH_DB is required.");
}

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IIdentificationStore, IdentificationStore>();
builder.Services.AddSingleton<IdentificationEventProcessor>();
builder.Services.AddSingleton<IdentificationEventPublisher>();
builder.Services.AddHostedService<IdentificationConsumerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapPost("/v1/identifications", async (
        IdentificationIngestRequest? request,
        HttpContext httpContext,
        IIdentificationStore store,
        IdentificationEventPublisher publisher,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        if (request is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["request"] = ["Identification request body is required."] },
                detail: "The identification payload is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid identification",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(request.StationId))
            errors["stationId"] = new[] { "Station id is required." };
        if (string.IsNullOrWhiteSpace(request.EventType))
            errors["eventType"] = new[] { "Event type is required." };
        if (request.OccurredAt == default)
            errors["occurredAt"] = new[] { "Occurred at is required." };

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                detail: "The identification payload is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid identification",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        var interaction = new CustomerStationInteraction(
            Guid.NewGuid().ToString("N"),
            request.StationId,
            request.TurnstileId ?? string.Empty,
            request.EventType,
            request.OccurredAt,
            request.MatchResult ?? "none",
            request.Confidence,
            request.PassengerId,
            request.OffenderId,
            Redacted: string.Equals(request.MatchResult, "none", StringComparison.OrdinalIgnoreCase)
                ? null
                : request.MatchResult);
        await store.RecordInteractionAsync(interaction, cancellationToken);
        await publisher.PublishInteractionCreatedAsync(interaction, cancellationToken);

        logger.LogInformation(
            "Identification recorded for interaction {InteractionId} station {StationId} match {MatchResult}",
            interaction.Id,
            interaction.StationId,
            interaction.MatchResult);

        return Results.Accepted($"/v1/identifications/{interaction.Id}", new
        {
            interaction.Id,
            status = "accepted",
            correlationId,
            recordedAt = DateTimeOffset.UtcNow
        });
    })
    .WithName("SubmitIdentification")
    .WithOpenApi();

app.MapGet("/v1/identifications/{interactionId}", async (
        string interactionId,
        IIdentificationStore store,
        CancellationToken cancellationToken) =>
    {
        var interaction = await store.GetInteractionAsync(interactionId, cancellationToken);
        return interaction is null
            ? Results.NotFound()
            : Results.Ok(interaction);
    })
    .WithName("GetIdentification")
    .WithOpenApi();

app.MapPost("/v1/identifications/{interactionId}/review", async (
        string interactionId,
        ReviewRequest? request,
        HttpContext httpContext,
        IIdentificationStore store,
        IdentificationEventPublisher publisher,
        CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        if (request is null ||
            !string.Equals(request.Decision, "approve", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(request.Decision, "reject", StringComparison.OrdinalIgnoreCase))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["decision"] = ["Decision must be 'approve' or 'reject'."] },
                detail: "The review decision is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid review decision",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        await store.RecordReviewAsync(new ReviewDecision(
            interactionId,
            request.Decision,
            request.Reviewer ?? "unknown",
            DateTimeOffset.UtcNow), cancellationToken);

        await publisher.PublishInteractionReviewedAsync(
            interactionId,
            request.Decision,
            request.Reviewer ?? "unknown",
            DateTimeOffset.UtcNow,
            cancellationToken);

        await store.RecordAuditAsync(
            new AuditEntry(
                Guid.NewGuid().ToString("N"),
                interactionId,
                $"review.{request.Decision.ToLower()}",
                request.Reviewer ?? "unknown",
                $"Review decision: {request.Decision}",
                DateTimeOffset.UtcNow),
            cancellationToken);

        return Results.Ok(new { interactionId, decision = request.Decision, recordedAt = DateTimeOffset.UtcNow });
    })
    .WithName("ReviewIdentification")
    .WithOpenApi();

app.MapGet("/v1/identifications/{interactionId}/review", async (
        string interactionId,
        IIdentificationStore store,
        CancellationToken cancellationToken) =>
    {
        var review = await store.GetReviewAsync(interactionId, cancellationToken);
        return review is null ? Results.NotFound() : Results.Ok(review);
    })
    .WithName("GetReview")
    .WithOpenApi();

app.MapPost("/v1/alerts", async (
        AlertNotification? alert,
        IIdentificationStore store,
        CancellationToken cancellationToken) =>
    {
        if (alert is null)
        {
            return Results.BadRequest(new { error = "Alert body is required." });
        }

        await store.RecordAlertAsync(alert, cancellationToken);
        return Results.Accepted($"/v1/alerts/{alert.Id}", alert);
    })
    .WithName("CreateAlert")
    .WithOpenApi();

app.MapGet("/v1/alerts", async (
        string? interactionId,
        IIdentificationStore store,
        CancellationToken cancellationToken) =>
    {
        var alerts = await store.GetAlertsAsync(interactionId, cancellationToken);
        return Results.Ok(alerts);
    })
    .WithName("ListAlerts")
    .WithOpenApi();

app.MapPost("/v1/purge", async (
        PurgeRequest? request,
        IIdentificationStore store,
        CancellationToken cancellationToken) =>
    {
        var olderThan = request?.OlderThan is not null
            ? DateTimeOffset.Parse(request.OlderThan)
            : DateTimeOffset.UtcNow.AddDays(-90);
        var reason = request?.Reason ?? "retention";

        var purged = await store.PurgeNonMatchOlderThanAsync(olderThan, reason, cancellationToken);

        await store.RecordAuditAsync(
            new AuditEntry(
                Guid.NewGuid().ToString("N"),
                "purge",
                "purge.nonmatch",
                "system",
                $"Purged {purged} non-match records older than {olderThan:O} ({reason})",
                DateTimeOffset.UtcNow),
            cancellationToken);

        return Results.Ok(new { purged, olderThan, reason, purgedAt = DateTimeOffset.UtcNow });
    })
    .WithName("PurgeNonMatch")
    .WithOpenApi();

app.MapGet("/v1/audit", async (
        string? interactionId,
        IIdentificationStore store,
        CancellationToken cancellationToken) =>
    {
        var entries = await store.GetAuditAsync(interactionId, cancellationToken);
        return Results.Ok(entries);
    })
    .WithName("ListAudit")
    .WithOpenApi();

app.MapPost("/v1/audit", async (
        AuditEntry? entry,
        IIdentificationStore store,
        CancellationToken cancellationToken) =>
    {
        if (entry is null)
        {
            return Results.BadRequest(new { error = "Audit entry body is required." });
        }

        await store.RecordAuditAsync(entry, cancellationToken);
        return Results.Accepted($"/v1/audit/{entry.Id}", entry);
    })
    .WithName("CreateAudit")
    .WithOpenApi();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .WithName("Liveness")
    .WithOpenApi();

app.MapGet("/health/ready", async (IIdentificationStore store, CancellationToken cancellationToken) =>
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

await app.Services.GetRequiredService<IIdentificationStore>().InitializeAsync();
app.Run();

public partial class Program;
