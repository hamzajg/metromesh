using System.Diagnostics;

namespace MetroMesh.Equipment.Registry.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "MetroMesh.CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var values) &&
                            !string.IsNullOrWhiteSpace(values.ToString())
            ? values.ToString()
            : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        var activity = Activity.Current;
        if (activity is not null)
        {
            activity.SetTag("correlation_id", correlationId);
            activity.SetTag("causation_id", activity.Id);
            activity.DisplayName = $"MetroMesh.{context.Request.Method} {context.Request.Path}";
        }

        await _next(context);
    }
}
