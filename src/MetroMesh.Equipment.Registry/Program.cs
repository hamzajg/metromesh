using System.Diagnostics;
using MetroMesh.Equipment.Registry.Contracts;
using MetroMesh.Equipment.Registry.Events;
using MetroMesh.Equipment.Registry.Middleware;
using MetroMesh.Equipment.Registry.Persistence;
using MetroMesh.Equipment.Registry.Validation;
using MetroMesh.Telemetry.Core.Events;
using MetroMesh.Telemetry.Core.Observability;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "MetroMesh Registry API", Version = "1.0.0" });
});

builder.Services.AddMetroMeshMetrics("registry");
builder.Services.AddMetroMeshServiceCatalog();

builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("RegistryDb")
        ?? builder.Configuration["METROMESH_REGISTRY_DB"];

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:RegistryDb or METROMESH_REGISTRY_DB is required.");
    }

    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddSingleton<IRegistryStore, PostgresRegistryStore>();
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

app.MapPost("/v1/stations", async (
        StationRequest? request,
        HttpContext httpContext,
        IRegistryStore store,
        CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        var errors = RegistryValidator.ValidateStation(request);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                detail: "The station payload is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid station",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        var station = await store.InsertStationAsync(
            request!.Name!,
            request.Address!,
            request.Status ?? "open",
            request.Location,
            cancellationToken);

        await RegistryEvents.EmitAsync(store, "station.created", station.Id, new
        {
            station.Name,
            station.Address,
            station.Status,
            station.Location
        }, correlationId, Activity.Current?.Id, cancellationToken);

        return Results.Created($"/v1/stations/{station.Id}", new StationResponse(
            station.Id,
            station.Name,
            station.Address,
            station.Status,
            station.Location,
            station.CreatedAt));
    })
    .WithName("RegisterStation")
    .WithOpenApi();

app.MapGet("/v1/stations/{stationId}", async (
        string stationId,
        IRegistryStore store,
        CancellationToken cancellationToken) =>
    {
        var station = await store.GetStationAsync(stationId, cancellationToken);
        return station is null
            ? Results.NotFound()
            : Results.Ok(new StationResponse(
                station.Id,
                station.Name,
                station.Address,
                station.Status,
                station.Location,
                station.CreatedAt));
    })
    .WithName("GetStation")
    .WithOpenApi();

app.MapPost("/v1/equipment", async (
        EquipmentRequest? request,
        HttpContext httpContext,
        IRegistryStore store,
        CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        var errors = RegistryValidator.ValidateEquipment(request);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                detail: "The equipment payload is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid equipment",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        var equipment = await store.InsertEquipmentAsync(
            request!.Kind!,
            request.Model!,
            request.StationId!,
            request.InstallDate,
            request.MaintenanceThreshold,
            request.Status ?? "active",
            cancellationToken);

        if (equipment is null)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]> { ["stationId"] = ["Station does not exist."] },
                detail: "The equipment references an unknown station.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Unknown station",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        await RegistryEvents.EmitAsync(store, $"equipment.{equipment.Kind}.installed", equipment.Id, new
        {
            equipment.Kind,
            equipment.Model,
            equipment.StationId,
            equipment.Status
        }, correlationId, Activity.Current?.Id, cancellationToken);

        return Results.Created($"/v1/equipment/{equipment.Id}", new EquipmentResponse(
            equipment.Id,
            equipment.Kind,
            equipment.Model,
            equipment.StationId,
            equipment.InstallDate,
            equipment.MaintenanceThreshold,
            equipment.Status,
            equipment.CreatedAt));
    })
    .WithName("RegisterEquipment")
    .WithOpenApi();

app.MapGet("/v1/equipment/{equipmentId}", async (
        string equipmentId,
        IRegistryStore store,
        CancellationToken cancellationToken) =>
    {
        var equipment = await store.GetEquipmentAsync(equipmentId, cancellationToken);
        return equipment is null
            ? Results.NotFound()
            : Results.Ok(new EquipmentResponse(
                equipment.Id,
                equipment.Kind,
                equipment.Model,
                equipment.StationId,
                equipment.InstallDate,
                equipment.MaintenanceThreshold,
                equipment.Status,
                equipment.CreatedAt));
    })
    .WithName("GetEquipment")
    .WithOpenApi();

app.MapPatch("/v1/stations/{stationId}", async (
        string stationId,
        StationStatusUpdateRequest? request,
        HttpContext httpContext,
        IRegistryStore store,
        CancellationToken cancellationToken) =>
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        var errors = RegistryValidator.ValidateStationStatusUpdate(request?.Status);

        if (errors.Count > 0)
        {
            return Results.ValidationProblem(
                errors,
                detail: "The station status update is invalid.",
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid station status",
                extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
        }

        var station = await store.UpdateStationStatusAsync(stationId, request!.Status!, cancellationToken);
        if (station is not null)
        {
            await RegistryEvents.EmitAsync(store, "station.status.changed", station.Id, new
            {
                station.Status
            }, correlationId, Activity.Current?.Id, cancellationToken);
        }

        return station is null
            ? Results.NotFound()
            : Results.Ok(new StationResponse(
                station.Id,
                station.Name,
                station.Address,
                station.Status,
                station.Location,
                station.CreatedAt));
    })
    .WithName("UpdateStationStatus")
    .WithOpenApi();

app.MapGet("/v1/stations/{stationId}/equipment", async (
        string stationId,
        IRegistryStore store,
        CancellationToken cancellationToken) =>
    {
        var station = await store.GetStationAsync(stationId, cancellationToken);
        if (station is null)
        {
            return Results.NotFound();
        }

        var items = await store.ListEquipmentByStationAsync(stationId, cancellationToken);
        return Results.Ok(items.Select(e => new EquipmentResponse(
            e.Id,
            e.Kind,
            e.Model,
            e.StationId,
            e.InstallDate,
            e.MaintenanceThreshold,
            e.Status,
            e.CreatedAt)).ToList());
    })
    .WithName("ListStationEquipment")
    .WithOpenApi();

app.MapPost("/v1/equipment/{equipmentId}/decommission", async (
        string equipmentId,
        HttpContext httpContext,
        IRegistryStore store,
        CancellationToken cancellationToken) =>
    {
        var equipment = await store.UpdateEquipmentStatusAsync(equipmentId, "decommissioned", cancellationToken);
        if (equipment is not null)
        {
            var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
            await RegistryEvents.EmitAsync(store, $"equipment.{equipment.Kind}.decommissioned", equipment.Id, new
            {
                equipment.Kind,
                equipment.StationId,
                equipment.Status
            }, correlationId, Activity.Current?.Id, cancellationToken);
        }

        return equipment is null
            ? Results.NotFound()
            : Results.Ok(new EquipmentResponse(
                equipment.Id,
                equipment.Kind,
                equipment.Model,
                equipment.StationId,
                equipment.InstallDate,
                equipment.MaintenanceThreshold,
                equipment.Status,
                equipment.CreatedAt));
    })
    .WithName("DecommissionEquipment")
    .WithOpenApi();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .WithName("Liveness")
    .WithOpenApi();

app.MapGet("/health/ready", async (IRegistryStore store, CancellationToken cancellationToken) =>
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

var catalogRegistry = app.Services.GetRequiredService<IServiceCatalog>();
catalogRegistry.RegisterService("registry", "1.0.0", "http://localhost:8082", new Dictionary<string, string>
{
    ["protocol"] = "http",
    ["port"] = "8082"
});

await app.Services.GetRequiredService<IRegistryStore>().InitializeAsync();
app.Run();

public partial class Program;
