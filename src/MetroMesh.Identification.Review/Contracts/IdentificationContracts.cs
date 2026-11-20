namespace MetroMesh.Identification.Review.Contracts;

public sealed record CustomerStationInteraction(
    string Id,
    string StationId,
    string TurnstileId,
    string EventType,
    DateTimeOffset OccurredAt,
    string MatchResult,
    double? Confidence,
    string? PassengerId,
    string? OffenderId,
    string? Redacted);

public sealed record IdentificationIngestRequest(
    string StationId,
    string? TurnstileId,
    string EventType,
    DateTimeOffset OccurredAt,
    string? MatchResult,
    double? Confidence,
    string? PassengerId,
    string? OffenderId);

public sealed record AlertNotification(
    string Id,
    string InteractionId,
    string MatchResult,
    string Recipient,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record AuditEntry(
    string Id,
    string InteractionId,
    string Action,
    string? Actor,
    string? Details,
    DateTimeOffset CreatedAt);

public sealed record ReviewDecision(
    string InteractionId,
    string Decision,
    string Reviewer,
    DateTimeOffset ReviewedAt);

public sealed record PurgeRecord(
    string InteractionId,
    string Reason,
    int RecordsPurged,
    DateTimeOffset PurgedAt);

public sealed record ReviewRequest(
    string InteractionId,
    string Decision,
    string Reviewer);

public sealed record PurgeRequest(
    string? OlderThan,
    string Reason);
