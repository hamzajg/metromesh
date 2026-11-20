using System.Text.Json;

namespace MetroMesh.Identification.Review.Middleware;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string? _apiKey;

    public ApiKeyMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        _apiKey = configuration["MetroMesh:ApiKey"];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        if (!path.StartsWith("/v1/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var providedKey = context.Request.Headers["X-MetroMesh-Api-Key"].ToString();
        if (string.IsNullOrWhiteSpace(_apiKey) ||
            string.IsNullOrWhiteSpace(providedKey) ||
            !string.Equals(providedKey, _apiKey, StringComparison.Ordinal))
        {
            var correlationId = context.Items[CorrelationIdMiddleware.ItemKey] as string ?? string.Empty;
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/problem+json";

            var problem = new
            {
                type = "https://tools.ietf.org/html/rfc9110#section-15.5.2",
                title = "Unauthorized",
                status = StatusCodes.Status401Unauthorized,
                detail = "A valid MetroMesh API key is required.",
                correlationId
            };

            await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            return;
        }

        await _next(context);
    }
}
