using MetroMesh.Maintenance.Contracts;
using MetroMesh.Maintenance.Middleware;
using MetroMesh.Maintenance.Persistence;
using MetroMesh.Maintenance.Services;
using MetroMesh.Maintenance.Validation;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton(_ =>
{
    var connectionString = builder.Configuration.GetConnectionString("MaintenanceDb")
        ?? builder.Configuration["METROMESH_MAINTENANCE_DB"];

    if (string.IsNullOrWhiteSpace(connectionString))
    {
        throw new InvalidOperationException("ConnectionStrings:MaintenanceDb or METROMESH_MAINTENANCE_DB is required.");
    }

    return NpgsqlDataSource.Create(connectionString);
});
builder.Services.AddSingleton<IMaintenanceStore, PostgresMaintenanceStore>();

var registryBaseUrl = builder.Configuration["Maintenance:Registry:BaseUrl"] ?? "http://localhost:8082";
var registryApiKey = builder.Configuration["Maintenance:Registry:ApiKey"]
    ?? builder.Configuration["MetroMesh:ApiKey"];
builder.Services.AddHttpClient<IEquipmentDirectory, HttpEquipmentDirectory>(client =>
{
    client.BaseAddress = new Uri(registryBaseUrl.TrimEnd('/') + "/");
    if (!string.IsNullOrWhiteSpace(registryApiKey))
    {
        client.DefaultRequestHeaders.Add("X-MetroMesh-Api-Key", registryApiKey);
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ApiKeyMiddleware>();

static IResult ValidationError(HttpContext httpContext, string title, string detail, IReadOnlyList<string> errors)
{
    var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
    return Results.ValidationProblem(
        errors.Select(e => $"General: {e}").GroupBy(_ => "general").ToDictionary(g => g.Key, g => g.ToArray()),
        detail: detail,
        statusCode: StatusCodes.Status400BadRequest,
        title: title,
        extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
}

async Task<IResult?> EnsureEquipmentAsync(
    IEquipmentDirectory directory,
    HttpContext httpContext,
    string equipmentId,
    CancellationToken cancellationToken)
{
    EquipmentDirectoryEntry? equipment;
    try
    {
        equipment = await directory.GetEquipmentAsync(equipmentId, cancellationToken);
    }
    catch (Exception)
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        return Results.Problem(
            detail: "The equipment registry is unavailable.",
            statusCode: StatusCodes.Status503ServiceUnavailable,
            title: "Registry unavailable",
            extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
    }

    if (equipment is null)
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        return Results.Problem(
            detail: "The referenced equipment does not exist in the registry.",
            statusCode: StatusCodes.Status400BadRequest,
            title: "Unknown equipment",
            extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
    }

    if (string.Equals(equipment.Status, "decommissioned", StringComparison.OrdinalIgnoreCase))
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
        return Results.Problem(
            detail: "The referenced equipment is decommissioned.",
            statusCode: StatusCodes.Status409Conflict,
            title: "Equipment decommissioned",
            extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
    }

    return null;
}

app.MapPost("/v1/maintenance-requests", async (
        CreateMaintenanceRequest? request,
        HttpContext httpContext,
        IMaintenanceStore store,
        IEquipmentDirectory directory,
        CancellationToken cancellationToken) =>
    {
        var errors = MaintenanceValidator.Validate(request);
        if (errors.Count > 0)
        {
            return ValidationError(httpContext, "Invalid maintenance request", "The maintenance request payload is invalid.", errors);
        }

        var equipmentError = await EnsureEquipmentAsync(directory, httpContext, request!.EquipmentId, cancellationToken);
        if (equipmentError is not null)
        {
            return equipmentError;
        }

        var record = await store.InsertRequestAsync(
            request.EquipmentId,
            request.Reason!,
            request.Severity!,
            request.Source ?? "manual",
            request.Details,
            cancellationToken);

        return Results.Created($"/v1/maintenance-requests/{record.Id}", new MaintenanceRequestResponse(
            record.Id,
            record.EquipmentId,
            record.Reason,
            record.Severity,
            record.Source,
            record.Details,
            record.Status,
            record.CreatedAt));
    })
    .WithName("CreateMaintenanceRequest")
    .WithOpenApi();

app.MapGet("/v1/maintenance-requests/{requestId}", async (
        string requestId,
        IMaintenanceStore store,
        CancellationToken cancellationToken) =>
    {
        var record = await store.GetRequestAsync(requestId, cancellationToken);
        return record is null
            ? Results.NotFound()
            : Results.Ok(new MaintenanceRequestResponse(
                record.Id,
                record.EquipmentId,
                record.Reason,
                record.Severity,
                record.Source,
                record.Details,
                record.Status,
                record.CreatedAt));
    })
    .WithName("GetMaintenanceRequest")
    .WithOpenApi();

app.MapPost("/v1/work-orders", async (
        CreateWorkOrder? request,
        HttpContext httpContext,
        IMaintenanceStore store,
        IEquipmentDirectory directory,
        CancellationToken cancellationToken) =>
    {
        var errors = MaintenanceValidator.Validate(request);
        if (errors.Count > 0)
        {
            return ValidationError(httpContext, "Invalid work order", "The work order payload is invalid.", errors);
        }

        var equipmentError = await EnsureEquipmentAsync(directory, httpContext, request!.EquipmentId, cancellationToken);
        if (equipmentError is not null)
        {
            return equipmentError;
        }

        if (!string.IsNullOrWhiteSpace(request.RequestId))
        {
            var linked = await store.GetRequestAsync(request.RequestId, cancellationToken);
            if (linked is null)
            {
                var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
                return Results.Problem(
                    detail: "The referenced maintenance request does not exist.",
                    statusCode: StatusCodes.Status400BadRequest,
                    title: "Unknown maintenance request",
                    extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
            }
        }

        var record = await store.InsertWorkOrderAsync(
            string.IsNullOrWhiteSpace(request.RequestId) ? null : request.RequestId,
            request.EquipmentId,
            request.Type!,
            request.Priority!,
            request.Title!,
            request.Description,
            cancellationToken);

        return Results.Created($"/v1/work-orders/{record.Id}", new WorkOrderResponse(
            record.Id,
            record.RequestId,
            record.EquipmentId,
            record.Type,
            record.Priority,
            record.Title,
            record.Description,
            record.Status,
            record.CreatedAt,
            record.UpdatedAt));
    })
    .WithName("CreateWorkOrder")
    .WithOpenApi();

app.MapGet("/v1/work-orders/{workOrderId}", async (
        string workOrderId,
        IMaintenanceStore store,
        CancellationToken cancellationToken) =>
    {
        var record = await store.GetWorkOrderAsync(workOrderId, cancellationToken);
        return record is null
            ? Results.NotFound()
            : Results.Ok(new WorkOrderResponse(
                record.Id,
                record.RequestId,
                record.EquipmentId,
                record.Type,
                record.Priority,
                record.Title,
                record.Description,
                record.Status,
                record.CreatedAt,
                record.UpdatedAt));
    })
    .WithName("GetWorkOrder")
    .WithOpenApi();

app.MapGet("/health/live", () => Results.Ok(new { status = "live" }))
    .WithName("Liveness")
    .WithOpenApi();

app.MapGet("/health/ready", async (IMaintenanceStore store, CancellationToken cancellationToken) =>
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

await app.Services.GetRequiredService<IMaintenanceStore>().InitializeAsync();
app.Run();

public partial class Program;
