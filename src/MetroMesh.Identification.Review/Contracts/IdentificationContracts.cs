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
