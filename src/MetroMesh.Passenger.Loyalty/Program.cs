using System.Diagnostics;
using System.Text.Json;
using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Passenger.Loyalty.Middleware;
using MetroMesh.Passenger.Loyalty.Persistence;
using MetroMesh.Passenger.Loyalty.Services;
using MetroMesh.Telemetry.Core.Observability;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "MetroMesh Passenger Loyalty API", Version = "1.0.0" });
});

builder.Services.AddMetroMeshMetrics("passenger-loyalty");
builder.Services.AddMetroMeshServiceCatalog();

var connectionString = builder.Configuration.GetConnectionString("PassengerDb")
    ?? builder.Configuration["METROMESH_DB"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException("ConnectionStrings:PassengerDb or METROMESH_DB is required.");
}

builder.Services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
builder.Services.AddSingleton<IPassengerStore, PassengerStore>();
builder.Services.AddSingleton<PassengerEventProcessor>();
builder.Services.AddSingleton<PassengerEventPublisher>();
builder.Services.AddHostedService<PassengerConsumerService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);

app.MapPost("/v1/passengers", async (
        PassengerProfileRequest? request,
        HttpContext httpContext,
        IPassengerStore store,
        PassengerEventPublisher publisher,
        ILogger<Program> logger,
        CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        if (request is null || string.IsNullOrWhiteSpace(request.ProfileReference))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["profileReference"] = ["Profile reference is required."] },
                detail: "The passenger profile request is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid passenger profile",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        var profile = new PassengerProfile(
            Guid.NewGuid().ToString("N"),
            request.ProfileReference,
            request.LoyaltyProgramReference,
            "active",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        await store.RecordPassengerAsync(profile, cancellationToken);
        await publisher.PublishPassengerCreatedAsync(profile, correlationId, Activity.Current?.Id, cancellationToken);

        logger.LogInformation(
            "Passenger profile created {PassengerId} reference {ProfileReference}",
            profile.Id,
            profile.ProfileReference);

        return Results.Accepted($"/v1/passengers/{profile.Id}", new
        {
            profile.Id,
            status = "accepted",
            correlationId,
            createdAt = DateTimeOffset.UtcNow
        });
    })
    .WithName("CreatePassenger")
    .WithOpenApi();

app.MapGet("/v1/passengers/{passengerId}", async (
        string passengerId,
        IPassengerStore store,
        CancellationToken cancellationToken) =>
    {
        var profile = await store.GetPassengerAsync(passengerId, cancellationToken);
        return profile is null ? Results.NotFound() : Results.Ok(profile);
    })
    .WithName("GetPassenger")
    .WithOpenApi();

app.MapPost("/v1/passengers/{passengerId}/info", async (
        string passengerId,
        PassengerInfoUpdate? update,
        HttpContext httpContext,
        IPassengerStore store,
        PassengerEventPublisher publisher,
        CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        if (update is null)
        {
            return Results.BadRequest(new { error = "Info update body is required." });
        }

        var existing = await store.GetPassengerAsync(passengerId, cancellationToken);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var info = new PassengerInfoRecord(
            Guid.NewGuid().ToString("N"),
            passengerId,
            update.ContactEmail,
            update.ContactPhone,
            update.Address,
            DateTimeOffset.UtcNow,
            existing.ProfileReference,
            null,
            null);

        await store.RecordPassengerInfoAsync(info, cancellationToken);
        await publisher.PublishPassengerUpdatedAsync(passengerId, update, DateTimeOffset.UtcNow, correlationId, Activity.Current?.Id, cancellationToken);

        return Results.Accepted($"/v1/passengers/{passengerId}/info/{info.Id}", new
        {
            info.Id,
            status = "accepted",
            correlationId,
            updatedAt = DateTimeOffset.UtcNow
        });
    })
    .WithName("UpdatePassengerInfo")
    .WithOpenApi();

app.MapGet("/v1/passengers/{passengerId}/info", async (
        string passengerId,
        IPassengerStore store,
        CancellationToken cancellationToken) =>
    {
        var history = await store.GetPassengerInfoAsync(passengerId, cancellationToken);
        return Results.Ok(history);
    })
    .WithName("GetPassengerInfo")
    .WithOpenApi();

app.MapPost("/v1/loyalty", async (
        LoyaltyProgramRequest? request,
        string passengerId,
        HttpContext httpContext,
        IPassengerStore store,
        CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        if (request is null || string.IsNullOrWhiteSpace(request.Tier))
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["tier"] = ["Tier is required."] },
                detail: "The loyalty program request is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid loyalty program",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        var program = new LoyaltyProgram(
            Guid.NewGuid().ToString("N"),
            passengerId,
            request.Tier,
            request.Status ?? "active",
            request.Terms,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            null);

        await store.RecordLoyaltyProgramAsync(program, cancellationToken);
        await store.RecordAuditAsync(
            new AuditEntry(
                Guid.NewGuid().ToString("N"),
                passengerId,
                "loyalty.created",
                null,
                $"Loyalty program {program.Id} created with tier {program.Tier}",
                DateTimeOffset.UtcNow),
            cancellationToken);

        return Results.Accepted($"/v1/loyalty/{program.Id}", new
        {
            program.Id,
            status = "accepted",
            correlationId,
            createdAt = DateTimeOffset.UtcNow
        });
    })
    .WithName("CreateLoyaltyProgram")
    .WithOpenApi();

app.MapGet("/v1/loyalty", async (
        string passengerId,
        IPassengerStore store,
        CancellationToken cancellationToken) =>
    {
        var programs = await store.GetLoyaltyProgramsAsync(passengerId, cancellationToken);
        return Results.Ok(programs);
    })
    .WithName("ListLoyaltyPrograms")
    .WithOpenApi();

app.MapPost("/v1/loyalty/{programId}/retire", async (
        string programId,
        IPassengerStore store,
        CancellationToken cancellationToken) =>
    {
        var program = await store.GetLoyaltyProgramAsync(programId, cancellationToken);
        if (program is null)
        {
            return Results.NotFound();
        }

        await store.RetireLoyaltyProgramAsync(programId, cancellationToken);
        await store.RecordAuditAsync(
            new AuditEntry(
                Guid.NewGuid().ToString("N"),
                program.PassengerId,
                "loyalty.retired",
                null,
                $"Loyalty program {programId} retired",
                DateTimeOffset.UtcNow),
            cancellationToken);

        return Results.Ok(new { programId, status = "retired", retiredAt = DateTimeOffset.UtcNow });
    })
    .WithName("RetireLoyaltyProgram")
    .WithOpenApi();

app.MapPost("/v1/notifications", async (
        NotificationRequest? request,
        IPassengerStore store,
        CancellationToken cancellationToken) =>
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Recipient))
        {
            return Results.BadRequest(new { error = "Notification recipient is required." });
        }

        var notification = new Notification(
            Guid.NewGuid().ToString("N"),
            request.PassengerId ?? string.Empty,
            request.Type ?? "profile.updated",
            request.Recipient,
            "pending",
            DateTimeOffset.UtcNow);

        await store.RecordNotificationAsync(notification, cancellationToken);
        await store.RecordAuditAsync(
            new AuditEntry(
                Guid.NewGuid().ToString("N"),
                notification.PassengerId,
                "notification.created",
                null,
                $"Notification {notification.Type} to {notification.Recipient}",
                DateTimeOffset.UtcNow),
            cancellationToken);

        return Results.Accepted($"/v1/notifications/{notification.Id}", notification);
    })
    .WithName("CreateNotification")
    .WithOpenApi();

app.MapGet("/v1/notifications", async (
        string? passengerId,
        IPassengerStore store,
        CancellationToken cancellationToken) =>
    {
        var notifications = await store.GetNotificationsAsync(passengerId, cancellationToken);
        return Results.Ok(notifications);
    })
    .WithName("ListNotifications")
    .WithOpenApi();

app.MapPost("/v1/audit", async (
        AuditEntry? entry,
        IPassengerStore store,
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

app.MapGet("/v1/audit", async (
        string? passengerId,
        IPassengerStore store,
        CancellationToken cancellationToken) =>
    {
        var entries = await store.GetAuditAsync(passengerId, cancellationToken);
        return Results.Ok(entries);
    })
    .WithName("ListAudit")
    .WithOpenApi();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .WithName("Liveness")
    .WithOpenApi();

app.MapGet("/health/ready", async (IPassengerStore store, CancellationToken cancellationToken) =>
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

var catalogPassenger = app.Services.GetRequiredService<IServiceCatalog>();
catalogPassenger.RegisterService("passenger-loyalty", "1.0.0", "http://localhost:8086", new Dictionary<string, string>
{
    ["protocol"] = "http",
    ["port"] = "8086"
});

await app.Services.GetRequiredService<IPassengerStore>().InitializeAsync();
app.Run();

public partial class Program;
