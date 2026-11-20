using System.Text.Json;
using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Passenger.Loyalty.Middleware;
using MetroMesh.Passenger.Loyalty.Persistence;
using MetroMesh.Passenger.Loyalty.Services;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

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
        await publisher.PublishPassengerCreatedAsync(profile, cancellationToken);

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
        await publisher.PublishPassengerUpdatedAsync(passengerId, update, DateTimeOffset.UtcNow, cancellationToken);

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

await app.Services.GetRequiredService<IPassengerStore>().InitializeAsync();
app.Run();

public partial class Program;
