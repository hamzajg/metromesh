using Confluent.Kafka;
using System.Text.Json;
using MetroMesh.Identification.Review.Contracts;
using MetroMesh.Telemetry.Core.Contracts;

namespace MetroMesh.Identification.Review.Services;

public sealed class IdentificationEventPublisher : IAsyncDisposable
{
    private readonly IProducer<Null, string> _producer;
    private readonly string _topic;
    private readonly ILogger<IdentificationEventPublisher> _logger;

    public IdentificationEventPublisher(IConfiguration configuration, ILogger<IdentificationEventPublisher> logger)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        _topic = configuration["Kafka:IdentificationTopic"] ?? "identification.review.v1";

        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers
        };

        _producer = new ProducerBuilder<Null, string>(config).Build();
        _logger = logger;
    }

    public async Task PublishInteractionCreatedAsync(CustomerStationInteraction interaction, string? correlationId = null, string? causationId = null, CancellationToken cancellationToken = default)
    {
        var envelope = new
        {
            specversion = "1.0",
            id = interaction.Id,
            type = "identification.interaction.created",
            source = "urn:metromesh:service:identification-review",
            subject = $"station/{interaction.StationId}",
            time = interaction.OccurredAt,
            datacontenttype = "application/json",
            correlationId,
            causationId,
            data = new
            {
                interactionId = interaction.Id,
                stationId = interaction.StationId,
                turnstileId = interaction.TurnstileId,
                eventType = interaction.EventType,
                occurredAt = interaction.OccurredAt,
                matchResult = interaction.MatchResult,
                confidence = interaction.Confidence,
                passengerId = interaction.PassengerId,
                offenderId = interaction.OffenderId,
                redacted = interaction.Redacted
            }
        };

        var json = JsonSerializer.Serialize(envelope);

        await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = json }, cancellationToken);

        _logger.LogInformation(
            "Published identification.interaction.created event {EventId} to topic {Topic}",
            interaction.Id,
            _topic);
    }

    public async Task PublishInteractionReviewedAsync(string interactionId, string decision, string reviewer, DateTimeOffset reviewedAt, string? correlationId = null, string? causationId = null, CancellationToken cancellationToken = default)
    {
        var envelope = new
        {
            specversion = "1.0",
            id = Guid.NewGuid().ToString("N"),
            type = "identification.interaction.reviewed",
            source = "urn:metromesh:service:identification-review",
            subject = $"interaction/{interactionId}",
            time = reviewedAt,
            datacontenttype = "application/json",
            correlationId,
            causationId,
            data = new
            {
                interactionId,
                decision,
                reviewer,
                reviewedAt
            }
        };

        var json = JsonSerializer.Serialize(envelope);

        await _producer.ProduceAsync(_topic, new Message<Null, string> { Value = json }, cancellationToken);

        _logger.LogInformation(
            "Published identification.interaction.reviewed event {EventId} to topic {Topic}",
            interactionId,
            _topic);
    }

    public ValueTask DisposeAsync()
    {
        _producer?.Dispose();
        return ValueTask.CompletedTask;
    }
}
