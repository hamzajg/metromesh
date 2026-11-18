using MetroMesh.Telemetry.Core.Events;
using MetroMesh.Telemetry.Core.Persistence;

namespace MetroMesh.Telemetry.Ingest.Events;

public sealed class KafkaOutboxPublisher : IOutboxPublisher
{
    private readonly ITelemetryEventStore _store;
    private readonly IEventBus _eventBus;
    private readonly string _topic;
    private readonly int _batchSize;
    private readonly ILogger<KafkaOutboxPublisher> _logger;

    public KafkaOutboxPublisher(
        ITelemetryEventStore store,
        IEventBus eventBus,
        IConfiguration configuration,
        ILogger<KafkaOutboxPublisher> logger)
    {
        _store = store;
        _eventBus = eventBus;
        _topic = configuration["Kafka:TelemetryTopic"] ?? "equipment.telemetry.v1";
        _batchSize = int.TryParse(configuration["Outbox:BatchSize"], out var batchSize) ? batchSize : 50;
        _logger = logger;
    }

    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken = default)
    {
        var pending = await _store.GetUnpublishedAsync(_batchSize, cancellationToken);
        var published = 0;

        foreach (var evt in pending)
        {
            try
            {
                await _eventBus.PublishAsync(_topic, evt.EventId, evt.EnvelopeJson, cancellationToken);
                await _store.MarkPublishedAsync(evt.EventId, cancellationToken);
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await _store.MarkPublishFailedAsync(evt.EventId, ex.Message, cancellationToken);
                _logger.LogWarning(ex, "Failed to publish telemetry event {EventId} to {Topic}", evt.EventId, _topic);
            }
        }

        return published;
    }
}
