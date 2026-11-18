using System.Net;
using System.Text;
using MetroMesh.Telemetry.Core.Persistence;

namespace MetroMesh.Telemetry.Worker.Services;

public sealed class WorkerHealthService : BackgroundService
{
    private readonly ITelemetryEventStore _store;
    private readonly string _prefix;
    private readonly ILogger<WorkerHealthService> _logger;

    public WorkerHealthService(
        ITelemetryEventStore store,
        IConfiguration configuration,
        ILogger<WorkerHealthService> logger)
    {
        _store = store;
        _logger = logger;
        _prefix = configuration["Worker:HealthUrl"] ?? "http://0.0.0.0:8081/";
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
            _logger.LogError(ex, "Worker health listener failed to start on {Prefix}", _prefix);
            return;
        }

        _logger.LogInformation("Worker health listening on {Prefix}", _prefix);

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
            _logger.LogWarning(ex, "Worker health request failed");
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
