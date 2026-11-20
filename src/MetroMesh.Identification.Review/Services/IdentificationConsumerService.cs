using Confluent.Kafka;
using MetroMesh.Identification.Review.Persistence;
using MetroMesh.Identification.Review.Services;

namespace MetroMesh.Identification.Review.Services;

public sealed class IdentificationConsumerService : BackgroundService
{
    private readonly IdentificationEventProcessor _processor;
    private readonly IIdentificationStore _store;
    private readonly IConfiguration _configuration;
    private readonly ILogger<IdentificationConsumerService> _logger;
    private readonly int _maxAttempts;

    public IdentificationConsumerService(
        IdentificationEventProcessor processor,
        IIdentificationStore store,
        IConfiguration configuration,
        ILogger<IdentificationConsumerService> logger)
    {
        _processor = processor;
        _store = store;
        _configuration = configuration;
        _logger = logger;
        _maxAttempts = int.TryParse(configuration["Identification:MaxAttempts"], out var maxAttempts) ? maxAttempts : 3;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bootstrapServers = _configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var topic = _configuration["Kafka:IdentificationTopic"] ?? "identification.review.v1";
        var groupId = _configuration["Kafka:ConsumerGroup"] ?? "identification-review-worker";

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
            "Identification consumer subscribed to {Topic} via {BootstrapServers} in group {GroupId}",
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
                    _logger.LogError(
                        ex,
                        "Failed to process identification event {EventId} at offset {Offset} on topic {Topic}; offset will not be committed",
                        eventId,
                        result.Offset.Value,
                        topic);
                }
            }
        }
        finally
        {
            consumer.Close();
        }
    }
}
