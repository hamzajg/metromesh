using System.Collections.Concurrent;

namespace MetroMesh.Telemetry.Core.Resilience;

public sealed class CircuitBreakerPolicy : ICircuitBreakerPolicy
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _resetTimeout;
    private readonly ConcurrentDictionary<string, CircuitBreakerState> _states = new();
    private readonly ConcurrentDictionary<string, int> _failures = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _lastFailureTime = new();

    public CircuitBreakerPolicy(int failureThreshold = 5, TimeSpan? resetTimeout = null)
    {
        _failureThreshold = failureThreshold;
        _resetTimeout = resetTimeout ?? TimeSpan.FromSeconds(30);
    }

    public CircuitBreakerState State => CircuitBreakerState.Closed;

    public CircuitBreakerState GetState(string downstreamService)
    {
        if (!_states.TryGetValue(downstreamService, out var state))
        {
            return CircuitBreakerState.Closed;
        }

        if (state == CircuitBreakerState.Open)
        {
            if (_lastFailureTime.TryGetValue(downstreamService, out var lastFailure) &&
                DateTimeOffset.UtcNow - lastFailure >= _resetTimeout)
            {
                _states[downstreamService] = CircuitBreakerState.HalfOpen;
                return CircuitBreakerState.HalfOpen;
            }
            return CircuitBreakerState.Open;
        }

        return state;
    }

    public async Task<T> ExecuteAsync<T>(Func<Task<T>> action, string downstreamService, CancellationToken cancellationToken = default)
    {
        var state = GetState(downstreamService);
        if (state == CircuitBreakerState.Open)
        {
            throw new CircuitBreakerOpenException(downstreamService);
        }

        try
        {
            var result = await action();
            OnSuccess(downstreamService);
            return result;
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not CircuitBreakerOpenException)
        {
            OnFailure(downstreamService);
            throw;
        }
    }

    public async Task ExecuteAsync(Func<Task> action, string downstreamService, CancellationToken cancellationToken = default)
    {
        var state = GetState(downstreamService);
        if (state == CircuitBreakerState.Open)
        {
            throw new CircuitBreakerOpenException(downstreamService);
        }

        try
        {
            await action();
            OnSuccess(downstreamService);
        }
        catch (Exception ex) when (ex is not OperationCanceledException && ex is not CircuitBreakerOpenException)
        {
            OnFailure(downstreamService);
            throw;
        }
    }

    private void OnSuccess(string downstreamService)
    {
        _states[downstreamService] = CircuitBreakerState.Closed;
        _failures.TryRemove(downstreamService, out _);
        _lastFailureTime.TryRemove(downstreamService, out _);
    }

    private void OnFailure(string downstreamService)
    {
        var failures = _failures.AddOrUpdate(downstreamService, 1, (_, v) => v + 1);
        _lastFailureTime[downstreamService] = DateTimeOffset.UtcNow;

        if (failures >= _failureThreshold)
        {
            _states[downstreamService] = CircuitBreakerState.Open;
        }
    }
}

public sealed class CircuitBreakerOpenException : Exception
{
    public string DownstreamService { get; }

    public CircuitBreakerOpenException(string downstreamService)
        : base($"Circuit breaker is open for downstream service: {downstreamService}")
    {
        DownstreamService = downstreamService;
    }
}
