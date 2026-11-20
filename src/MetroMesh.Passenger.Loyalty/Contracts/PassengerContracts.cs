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

public sealed record LoyaltyProgram(
    string Id,
    string PassengerId,
    string Tier,
    string Status,
    string Terms,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? RetiredAt);

public sealed record LoyaltyProgramRequest(
    string Tier,
    string Status,
    string Terms);

public sealed record Notification(
    string Id,
    string PassengerId,
    string Type,
    string Recipient,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record NotificationRequest(
    string PassengerId,
    string Type,
    string Recipient);

public sealed record AuditEntry(
    string Id,
    string PassengerId,
    string Action,
    string? Actor,
    string? Details,
    DateTimeOffset CreatedAt);
