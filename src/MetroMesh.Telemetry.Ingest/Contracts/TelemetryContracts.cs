using System.Text.Json;

namespace MetroMesh.Telemetry.Ingest.Contracts;

public sealed record TelemetryEventRequest(
    string? SpecVersion,
    string? Id,
    string? Type,
    string? Source,
    string? Subject,
    DateTimeOffset? Time,
    string? DataContentType,
    TelemetryEventData? Data);

public sealed record TelemetryEventData(
    string? EquipmentId,
    string? StationId,
    string? EventType,
    DateTimeOffset? OccurredAt,
    JsonElement? Payload);

public sealed record AcceptedEventResponse(
    string EventId,
    string Status,
    string CorrelationId,
    DateTimeOffset AcceptedAt);

public sealed record TelemetryEventEnvelope(
    string SpecVersion,
    string Id,
    string Type,
    string Source,
    string Subject,
    DateTimeOffset Time,
    string DataContentType,
    string CorrelationId,
    TelemetryEventData Data);

public sealed record TelemetryEventStatusResponse(
    string EventId,
    string Status,
    DateTimeOffset AcceptedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ProcessedAt,
    DateTimeOffset? DeadLetteredAt,
    int Attempts,
    string? LastError);
