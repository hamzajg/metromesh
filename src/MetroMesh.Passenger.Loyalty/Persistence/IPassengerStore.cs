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
}
