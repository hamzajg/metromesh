using MetroMesh.Maintenance.Persistence;
using MetroMesh.Telemetry.Core.Events;

namespace MetroMesh.Maintenance.Events;

public sealed class KafkaOutboxPublisher : IOutboxPublisher
{
    private readonly IMaintenanceStore _store;
    private readonly IEventBus _eventBus;
    private readonly string _topic;
    private readonly int _batchSize;
    private readonly ILogger<KafkaOutboxPublisher> _logger;

    public KafkaOutboxPublisher(
        IMaintenanceStore store,
        IEventBus eventBus,
        IConfiguration configuration,
        ILogger<KafkaOutboxPublisher> logger)
    {
        _store = store;
        _eventBus = eventBus;
        _topic = configuration["Kafka:MaintenanceTopic"] ?? "maintenance.workorder.v1";
        _batchSize = int.TryParse(configuration["Outbox:BatchSize"], out var batchSize) ? batchSize : 50;
        _logger = logger;
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _store.GetUnpublishedOutboxAsync(_batchSize, cancellationToken);
        var published = 0;

        foreach (var evt in pending)
        {
            try
            {
                await _eventBus.PublishAsync(_topic, evt.AggregateId, evt.EnvelopeJson, cancellationToken);
                await _store.MarkOutboxPublishedAsync(evt.Id, cancellationToken);
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await _store.MarkOutboxFailedAsync(evt.Id, ex.Message, cancellationToken);
                _logger.LogWarning(ex, "Failed to publish maintenance event {EventId} to {Topic}", evt.Id, _topic);
            }
        }

        return published;
    }
}
