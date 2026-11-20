using MetroMesh.Passenger.Loyalty.Contracts;
using MetroMesh.Passenger.Loyalty.Persistence;

namespace MetroMesh.Passenger.Loyalty.Tests;

public sealed class FakePassengerStore : IPassengerStore
{
    private readonly Dictionary<string, PassengerProfile> _passengers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<PassengerInfoRecord>> _infoHistory = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LoyaltyProgram> _loyaltyPrograms = new(StringComparer.Ordinal);
    private readonly List<Notification> _notifications = new();
    private readonly List<AuditEntry> _audit = new();
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

    public Task RecordLoyaltyProgramAsync(LoyaltyProgram program, CancellationToken cancellationToken = default)
    {
        _loyaltyPrograms[program.Id] = program;
        return Task.CompletedTask;
    }

    public Task<LoyaltyProgram?> GetLoyaltyProgramAsync(string programId, CancellationToken cancellationToken = default)
    {
        _loyaltyPrograms.TryGetValue(programId, out var program);
        return Task.FromResult(program);
    }

    public Task<IReadOnlyList<LoyaltyProgram>> GetLoyaltyProgramsAsync(string passengerId, CancellationToken cancellationToken = default)
    {
        var programs = _loyaltyPrograms.Values.Where(p => p.PassengerId == passengerId).ToList();
        return Task.FromResult<IReadOnlyList<LoyaltyProgram>>(programs);
    }

    public Task RetireLoyaltyProgramAsync(string programId, CancellationToken cancellationToken = default)
    {
        if (_loyaltyPrograms.TryGetValue(programId, out var program))
        {
            _loyaltyPrograms[programId] = program with
            {
                Status = "retired",
                RetiredAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
        }

        return Task.CompletedTask;
    }

    public Task RecordNotificationAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        _notifications.Add(notification);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Notification>> GetNotificationsAsync(string? passengerId, CancellationToken cancellationToken = default)
    {
        var filtered = passengerId is not null
            ? _notifications.Where(n => n.PassengerId == passengerId).ToList()
            : _notifications.ToList();
        return Task.FromResult<IReadOnlyList<Notification>>(filtered);
    }

    public Task RecordAuditAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        _audit.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEntry>> GetAuditAsync(string? passengerId, CancellationToken cancellationToken = default)
    {
        var filtered = passengerId is not null
            ? _audit.Where(a => a.PassengerId == passengerId).ToList()
            : _audit.ToList();
        return Task.FromResult<IReadOnlyList<AuditEntry>>(filtered);
    }

    public IReadOnlyList<PassengerProfile> Passengers => _passengers.Values.ToList();
    public IReadOnlyDictionary<string, List<PassengerInfoRecord>> InfoHistory => _infoHistory;
    public IReadOnlyDictionary<string, LoyaltyProgram> LoyaltyPrograms => _loyaltyPrograms;
    public IReadOnlyList<Notification> Notifications => _notifications;
    public IReadOnlyList<AuditEntry> AuditEntries => _audit;
}
