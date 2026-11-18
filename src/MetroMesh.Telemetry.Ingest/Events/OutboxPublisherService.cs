namespace MetroMesh.Telemetry.Ingest.Events;

public sealed class OutboxPublisherService : BackgroundService
{
    private readonly IOutboxPublisher _publisher;
    private readonly bool _enabled;
    private readonly int _pollIntervalMs;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IOutboxPublisher publisher,
        IConfiguration configuration,
        ILogger<OutboxPublisherService> logger)
    {
        _publisher = publisher;
        _enabled = !bool.TryParse(configuration["Outbox:Enabled"], out var enabled) || enabled;
        _pollIntervalMs = int.TryParse(configuration["Outbox:PollIntervalMs"], out var interval) ? interval : 1000;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _publisher.PublishPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox publisher iteration failed");
            }

            try
            {
                await Task.Delay(_pollIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
