namespace MetroMesh.Analytics.Contracts;

public sealed record RidershipReading(
    string EventId,
    string StationId,
    string EquipmentId,
    string EventType,
    DateTimeOffset OccurredAt,
    int PassengerCount);
