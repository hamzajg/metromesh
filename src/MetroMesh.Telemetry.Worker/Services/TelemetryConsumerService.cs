using Confluent.Kafka;
using MetroMesh.Telemetry.Core.Persistence;
using MetroMesh.Telemetry.Worker.Services;

namespace MetroMesh.Telemetry.Worker.Services;

public sealed class TelemetryConsumerService : BackgroundService
{
    private readonly TelemetryEventProcessor _processor;
    private readonly ITelemetryEventStore _store;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TelemetryConsumerService> _logger;
    private readonly int _maxAttempts;

    public TelemetryConsumerService(
        TelemetryEventProcessor processor,
        ITelemetryEventStore store,
        IConfiguration configuration,
        ILogger<TelemetryConsumerService> logger)
    {
        _processor = processor;
        _store = store;
        _configuration = configuration;
        _logger = logger;
        _maxAttempts = int.TryParse(configuration["Worker:MaxAttempts"], out var maxAttempts) ? maxAttempts : 3;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var topic = _configuration["Kafka:TelemetryTopic"] ?? "equipment.telemetry.v1";
        var groupId = _configuration["Kafka:ConsumerGroup"] ?? "telemetry-worker";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = groupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(topic);
        _logger.LogInformation(
            "Telemetry consumer subscribed to {Topic} via {BootstrapServers} in group {GroupId}",
            topic,
            bootstrapServers,
            groupId);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, string> result;
                try
                {
                    result = consumer.Consume(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (ConsumeException ex)
                {
                    _logger.LogError(ex, "Kafka consume failed on topic {Topic}", topic);
                    await Task.Delay(1000, stoppingToken);
                    continue;
                }

                try
                {
                    await _processor.ProcessAsync(result.Message.Value, stoppingToken);
                    consumer.Commit(result);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    var eventId = result.Message.Key ?? "unknown";
                    var deadLettered = false;
                    try
                    {
                        deadLettered = await _store.RecordProcessingFailureAsync(
                            eventId,
                            ex.Message,
                            _maxAttempts,
                            stoppingToken);
                    }
                    catch (Exception recordEx) when (recordEx is not OperationCanceledException)
                    {
                        _logger.LogError(recordEx, "Failed to record processing failure for telemetry event {EventId}", eventId);
                    }

                    if (deadLettered)
                    {
                        consumer.Commit(result);
                        _logger.LogError(
                            ex,
                            "Telemetry event {EventId} dead-lettered after {MaxAttempts} attempts at offset {Offset} on topic {Topic}",
                            eventId,
                            _maxAttempts,
                            result.Offset.Value,
                            topic);
                    }
                    else
                    {
                        _logger.LogError(
                            ex,
                            "Failed to process telemetry event {EventId} at offset {Offset} on topic {Topic}; offset will not be committed",
                            eventId,
                            result.Offset.Value,
                            topic);
                    }
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
