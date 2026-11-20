using System.Text.Json;
using Confluent.Kafka;
using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Telemetry.Core.Contracts;

namespace MetroMesh.Passenger.Loyalty.Services;

public sealed class PassengerEventPublisher : IAsyncDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;
    private readonly ILogger<PassengerEventPublisher> _logger;

    public PassengerEventPublisher(IConfiguration configuration, ILogger<PassengerEventPublisher> logger)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        _topic = configuration["Kafka:PassengerTopic"] ?? "passenger.loyalty.v1";

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
        _logger = logger;
    }

    public async Task PublishPassengerCreatedAsync(PassengerProfile profile, CancellationToken cancellationToken = default)
    {
        var envelope = new
        {
            specversion = "1.0",
            id = profile.Id,
            type = "passenger.profile.created",
            source = "urn:metromesh:service:passenger-loyalty",
            subject = $"passenger/{profile.Id}",
            time = profile.CreatedAt,
            datacontenttype = "application/json",
            data = new
            {
                passengerId = profile.Id,
                profileReference = profile.ProfileReference,
                loyaltyProgramReference = profile.LoyaltyProgramReference,
                status = profile.Status,
                createdAt = profile.CreatedAt,
                updatedAt = profile.UpdatedAt
            }
        };

        var json = JsonSerializer.Serialize(envelope);

        await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = json }, cancellationToken);

        _logger.LogInformation(
            "Published passenger.profile.created event {EventId} to topic {Topic}",
            profile.Id,
            _topic);
    }

    public async Task PublishPassengerUpdatedAsync(string passengerId, PassengerInfoUpdate update, DateTimeOffset updatedAt, CancellationToken cancellationToken = default)
    {
        var envelope = new
        {
            specversion = "1.0",
            id = Guid.NewGuid().ToString("N"),
            type = "passenger.info.updated",
            source = "urn:metromesh:service:passenger-loyalty",
            subject = $"passenger/{passengerId}",
            time = updatedAt,
            datacontenttype = "application/json",
            data = new
            {
                passengerId,
                contactEmail = update.ContactEmail,
                contactPhone = update.ContactPhone,
                address = update.Address,
                updatedAt
            }
        };

        var json = JsonSerializer.Serialize(envelope);

        await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = json }, cancellationToken);

        _logger.LogInformation(
            "Published passenger.info.updated event {PassengerId} to topic {Topic}",
            passengerId,
            _topic);
    }

    public ValueTask DisposeAsync()
    {
        _producer?.Dispose();
        return ValueTask.CompletedTask;
    }
}
