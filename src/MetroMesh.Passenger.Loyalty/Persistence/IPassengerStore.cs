using MetroMesh.Passenger.Loyalty.Contracts;

namespace MetroMesh.Passenger.Loyalty.Persistence;

public interface IPassengerStore
{
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default);
    Task RecordPassengerAsync(PassengerProfile profile, CancellationToken cancellationToken = default);
    Task<PassengerProfile?> GetPassengerAsync(string passengerId, CancellationToken cancellationToken = default);
    Task RecordPassengerInfoAsync(PassengerInfoRecord info, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PassengerInfoRecord>> GetPassengerInfoAsync(string passengerId, CancellationToken cancellationToken = default);
    Task<bool> PingAsync(CancellationToken cancellationToken = default);
    Task RecordLoyaltyProgramAsync(LoyaltyProgram program, CancellationToken cancellationToken = default);
    Task<LoyaltyProgram?> GetLoyaltyProgramAsync(string programId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoyaltyProgram>> GetLoyaltyProgramsAsync(string passengerId, CancellationToken cancellationToken = default);
    Task RetireLoyaltyProgramAsync(string programId, CancellationToken cancellationToken = default);
    Task RecordNotificationAsync(Notification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Notification>> GetNotificationsAsync(string? passengerId, CancellationToken cancellationToken = default);
    Task RecordAuditAsync(AuditEntry entry, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditEntry>> GetAuditAsync(string? passengerId, CancellationToken cancellationToken = default);
}
