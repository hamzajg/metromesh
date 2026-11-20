using System.Net;
using System.Text;
using MetroMesh.Analytics.Persistence;

namespace MetroMesh.Analytics.Services;

public sealed class AnalyticsHealthService : BackgroundService
{
    private readonly IAnalyticsStore _store;
    private readonly string _prefix;
    private readonly ILogger<AnalyticsHealthService> _logger;

    public AnalyticsHealthService(
        IAnalyticsStore store,
        IConfiguration configuration,
        ILogger<AnalyticsHealthService> logger)
    {
        _store = store;
        _logger = logger;
        _prefix = configuration["Analytics:HealthUrl"] ?? "http://0.0.0.0:8084/";
        if (!_prefix.EndsWith('/'))
        {
            _prefix += '/';
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var listener = new HttpListener();
        listener.Prefixes.Add(_prefix);

        try
        {
            listener.Start();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics health listener failed to start on {Prefix}", _prefix);
            return;
        }

        _logger.LogInformation("Analytics health listening on {Prefix}", _prefix);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                HttpListenerContext context;
                try
                {
                    context = await listener.GetContextAsync().WaitAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }

                await HandleContextAsync(context, stoppingToken);
            }
        }
        finally
        {
            listener.Stop();
            listener.Close();
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var (statusCode, body) = await HandleRequestAsync(
                context.Request.Url?.AbsolutePath ?? "/",
                cancellationToken);

            var bytes = Encoding.UTF8.GetBytes(body);
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = bytes.Length;
            await context.Response.OutputStream.WriteAsync(bytes, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Analytics health request failed");
        }
        finally
        {
            context.Response.Close();
        }
    }

    public async Task<(int StatusCode, string Body)> HandleRequestAsync(string path, CancellationToken cancellationToken = default)
    {
        if (path == "/health/live")
        {
            return (200, "{\"status\":\"live\"}");
        }

        if (path == "/health/ready")
        {
            try
            {
                var ready = await _store.PingAsync(cancellationToken);
                return ready
                    ? (200, "{\"status\":\"ready\"}")
                    : (503, "{\"status\":\"not_ready\"}");
            }
            catch
            {
                return (503, "{\"status\":\"not_ready\"}");
            }
        }

        return (404, "{\"status\":\"not_found\"}");
    }
}
