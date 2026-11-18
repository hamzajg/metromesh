using Confluent.Kafka;

namespace MetroMesh.Telemetry.Ingest.Events;

public sealed class ConfluentKafkaEventBus : IEventBus, IDisposable
{
    private readonly IProducer<string, string> _producer;

    public ConfluentKafkaEventBus(IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            EnableIdempotence = true,
            MessageTimeoutMs = 5000
        };

        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublishAsync(string topic, string key, string value, CancellationToken cancellationToken = default)
    {
        var completion = new TaskCompletionSource<DeliveryResult<string, string>>(TaskCreationOptions.RunContinuationsAsynchronously);

        _producer.Produce(
            topic,
            new Message<string, string> { Key = key, Value = value },
            report =>
            {
                if (report.Error.IsError)
                {
                    completion.TrySetException(new InvalidOperationException($"Kafka delivery failed: {report.Error.Reason}"));
                }
                else
                {
                    completion.TrySetResult(report);
                }
            });

        await completion.Task.WaitAsync(cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
