namespace MetroMesh.Passenger.Loyalty.Contracts;

public sealed record PassengerProfile(
    string Id,
    string ProfileReference,
    string? LoyaltyProgramReference,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record PassengerProfileRequest(
    string ProfileReference,
    string? LoyaltyProgramReference);

public sealed record PassengerInfoUpdate(
    string ContactEmail,
    string? ContactPhone,
    string? Address);

public sealed record PassengerInfoRecord(
    string Id,
    string PassengerId,
    string ContactEmail,
    string? ContactPhone,
    string? Address,
    DateTimeOffset UpdatedAt,
    string PreviousContactEmail,
    string? PreviousContactPhone,
    string? PreviousAddress);
