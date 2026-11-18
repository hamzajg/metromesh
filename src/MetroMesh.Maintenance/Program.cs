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

static WorkOrderResponse ToWorkOrderResponse(WorkOrderRecord record) => new(
    record.Id,
    record.RequestId,
    record.EquipmentId,
    record.Type,
    record.Priority,
    record.Title,
    record.Description,
    record.Assignee,
    record.Resolution,
    record.Status,
    record.CreatedAt,
    record.UpdatedAt);

var AllowedTransitions =
    new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
    {
        ["open"] = new HashSet<string>(StringComparer.Ordinal) { "assigned", "cancelled" },
        ["assigned"] = new HashSet<string>(StringComparer.Ordinal) { "in_progress", "cancelled" },
        ["in_progress"] = new HashSet<string>(StringComparer.Ordinal) { "completed", "cancelled" },
        ["completed"] = new HashSet<string>(StringComparer.Ordinal),
        ["cancelled"] = new HashSet<string>(StringComparer.Ordinal)
    };

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

        if (!string.IsNullOrWhiteSpace(request.RequestId))
        {
            await store.UpdateRequestStatusAsync(request.RequestId, "converted", cancellationToken);
        }

        return Results.Created($"/v1/work-orders/{record.Id}", ToWorkOrderResponse(record));
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
            : Results.Ok(ToWorkOrderResponse(record));
    })
    .WithName("GetWorkOrder")
    .WithOpenApi();

IResult TransitionResult(HttpContext httpContext, string? error, string? title = null)
{
    if (error is null)
    {
        return Results.Ok();
    }

    var correlationId = httpContext.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
    return Results.Problem(
        detail: error,
        statusCode: StatusCodes.Status409Conflict,
        title: title ?? "Invalid transition",
        extensions: new Dictionary<string, object?> { ["correlationId"] = correlationId });
}

async Task<IResult> TransitionAsync(
    string workOrderId,
    string toStatus,
    string? assignee,
    string? resolution,
    string? note,
    HttpContext httpContext,
    IMaintenanceStore store,
    CancellationToken cancellationToken)
{
    var current = await store.GetWorkOrderAsync(workOrderId, cancellationToken);
    if (current is null)
    {
        return Results.NotFound();
    }

    var allowed = AllowedTransitions.TryGetValue(current.Status, out var targets) && targets.Contains(toStatus);
    if (!allowed)
    {
        return TransitionResult(httpContext, $"Cannot transition work order from '{current.Status}' to '{toStatus}'.");
    }

    var updated = await store.TransitionWorkOrderAsync(workOrderId, toStatus, assignee, resolution, note, cancellationToken);
    return updated is null
        ? Results.NotFound()
        : Results.Ok(ToWorkOrderResponse(updated));
}

app.MapPost("/v1/work-orders/{workOrderId}/assign", async (
        string workOrderId,
        AssignWorkOrder? request,
        HttpContext httpContext,
        IMaintenanceStore store,
        CancellationToken cancellationToken) =>
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Assignee))
        {
            return ValidationError(httpContext, "Invalid assignment", "The assignment payload is invalid.", new[] { "assignee is required" });
        }

        return await TransitionAsync(workOrderId, "assigned", request.Assignee.Trim(), null, null, httpContext, store, cancellationToken);
    })
    .WithName("AssignWorkOrder")
    .WithOpenApi();

app.MapPost("/v1/work-orders/{workOrderId}/start", async (
        string workOrderId,
        HttpContext httpContext,
        IMaintenanceStore store,
        CancellationToken cancellationToken) =>
    await TransitionAsync(workOrderId, "in_progress", null, null, null, httpContext, store, cancellationToken))
    .WithName("StartWorkOrder")
    .WithOpenApi();

app.MapPost("/v1/work-orders/{workOrderId}/complete", async (
        string workOrderId,
        CompleteWorkOrder? request,
        HttpContext httpContext,
        IMaintenanceStore store,
        CancellationToken cancellationToken) =>
    await TransitionAsync(workOrderId, "completed", null, request?.Resolution, null, httpContext, store, cancellationToken))
    .WithName("CompleteWorkOrder")
    .WithOpenApi();

app.MapPost("/v1/work-orders/{workOrderId}/cancel", async (
        string workOrderId,
        CancelWorkOrder? request,
        HttpContext httpContext,
        IMaintenanceStore store,
        CancellationToken cancellationToken) =>
    await TransitionAsync(workOrderId, "cancelled", null, null, request?.Reason, httpContext, store, cancellationToken))
    .WithName("CancelWorkOrder")
    .WithOpenApi();

app.MapGet("/v1/work-orders", async (
        string? status,
        string? equipmentId,
        IMaintenanceStore store,
        CancellationToken cancellationToken) =>
    {
        var items = await store.ListWorkOrdersAsync(status, equipmentId, cancellationToken);
        return Results.Ok(items.Select(ToWorkOrderResponse).ToList());
    })
    .WithName("ListWorkOrders")
    .WithOpenApi();

app.MapGet("/v1/work-orders/{workOrderId}/history", async (
        string workOrderId,
        IMaintenanceStore store,
        CancellationToken cancellationToken) =>
    {
        var order = await store.GetWorkOrderAsync(workOrderId, cancellationToken);
        if (order is null)
        {
            return Results.NotFound();
        }

        var history = await store.ListWorkOrderHistoryAsync(workOrderId, cancellationToken);
        return Results.Ok(history.Select(h => new WorkOrderHistoryEntry(
            h.Id,
            h.WorkOrderId,
            h.FromStatus,
            h.ToStatus,
            h.Note,
            h.CreatedAt)).ToList());
    })
    .WithName("GetWorkOrderHistory")
    .WithOpenApi();

app.MapGet("/v1/maintenance-requests", async (
        string? status,
        string? equipmentId,
        IMaintenanceStore store,
        CancellationToken cancellationToken) =>
    {
        var items = await store.ListRequestsAsync(status, equipmentId, cancellationToken);
        return Results.Ok(items.Select(r => new MaintenanceRequestResponse(
            r.Id,
            r.EquipmentId,
            r.Reason,
            r.Severity,
            r.Source,
            r.Details,
            r.Status,
            r.CreatedAt)).ToList());
    })
    .WithName("ListMaintenanceRequests")
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
