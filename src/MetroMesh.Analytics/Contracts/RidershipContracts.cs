using System.Text.Json;

namespace MetroMesh.Analytics.Contracts;

public sealed record RidershipReading(
    string EventId,
    string StationId,
    string EquipmentId,
    string EventType,
    DateTimeOffset OccurredAt,
    int PassengerCount);

public sealed record RidershipCount(
    string StationId,
    string? EquipmentId,
    int Count);

public sealed record TimeBucketCount(
    DateTimeOffset BucketStart,
    int Count);
