using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Passenger.Loyalty.Persistence;

namespace MetroMesh.Passenger.Loyalty.Tests;

public sealed class FakePassengerStore : IPassengerStore
{
    private readonly Dictionary<string, PassengerProfile> _passengers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<PassengerInfoRecord>> _infoHistory = new(StringComparer.Ordinal);
    private readonly HashSet<string> _processed = new(StringComparer.Ordinal);

    public int ProcessedCount;
    public bool PingOk { get; set; } = true;

    public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<bool> TryMarkProcessedAsync(string eventId, CancellationToken cancellationToken = default)
    {
        if (_processed.Add(eventId))
        {
            Interlocked.Increment(ref ProcessedCount);
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    public Task RecordPassengerAsync(PassengerProfile profile, CancellationToken cancellationToken = default)
    {
        _passengers[profile.Id] = profile;
        return Task.CompletedTask;
    }

    public Task<PassengerProfile?> GetPassengerAsync(string passengerId, CancellationToken cancellationToken = default)
    {
        _passengers.TryGetValue(passengerId, out var passenger);
        return Task.FromResult(passenger);
    }

    public Task RecordPassengerInfoAsync(PassengerInfoRecord info, CancellationToken cancellationToken = default)
    {
        if (!_infoHistory.TryGetValue(info.PassengerId, out var history))
        {
            history = new List<PassengerInfoRecord>();
            _infoHistory[info.PassengerId] = history;
        }

        history.Add(info);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PassengerInfoRecord>> GetPassengerInfoAsync(string passengerId, CancellationToken cancellationToken = default)
    {
        _infoHistory.TryGetValue(passengerId, out var history);
        return Task.FromResult<IReadOnlyList<PassengerInfoRecord>>(history ?? new List<PassengerInfoRecord>());
    }

    public Task<bool> PingAsync(CancellationToken cancellationToken = default) => Task.FromResult(PingOk);

    public IReadOnlyList<PassengerProfile> Passengers => _passengers.Values.ToList();
    public IReadOnlyDictionary<string, List<PassengerInfoRecord>> InfoHistory => _infoHistory;
}
