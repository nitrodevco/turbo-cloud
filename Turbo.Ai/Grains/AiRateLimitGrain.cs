using System;
using System.Threading;
using System.Threading.Tasks;
using Orleans;

namespace Turbo.Ai;

/// <summary>
/// One cluster-wide rolling limit. Its minute of reservations is transient: activation loss
/// resets the window; no database state is owned or written by this grain.
/// </summary>
internal sealed class AiRateLimitGrain : Grain, IAiRateLimitGrain
{
    private readonly TimeProvider _clock;
    private readonly AiRateLimitGrainLiveState _state = new();

    public AiRateLimitGrain(TimeProvider clock)
    {
        _clock = clock;
    }

    public Task<bool> TryStartAsync(Guid reservation, int limit, CancellationToken ct)
    {
        var now = _clock.GetUtcNow();
        if (!_state.Window.Allows(limit, now))
            return Task.FromResult(false);

        _state.Window.Count(now, reservation);
        return Task.FromResult(true);
    }

    public Task UndoAsync(Guid reservation, CancellationToken ct)
    {
        _state.Window.Undo(reservation);
        return Task.CompletedTask;
    }
}
