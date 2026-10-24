using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Turbo.Rooms.Grains.Systems;

/// <summary>
/// Work an action leaves for later on the wired clock: Teleport To puts the user on the target
/// two seconds after the box fired, on the room's own wired time, not the wall clock.
/// </summary>
public sealed partial class RoomWiredSystem
{
    private readonly PriorityQueue<
        Func<CancellationToken, Task>,
        (long DueAtMs, long Order)
    > _deferred = new();
    private long _deferredOrder;
    private long _lastTickMs;

    /// <summary>The time <see cref="RunAfter"/> counts from.</summary>
    public long DeferredNowMs => _execution?.Now ?? _lastTickMs;

    /// <summary>
    /// Runs <paramref name="work"/> once, <paramref name="delayMs"/> after the execution running
    /// now (or the last wired tick), in the order it was asked for among work due together.
    /// </summary>
    public void RunAfter(int delayMs, Func<CancellationToken, Task> work)
    {
        var now = DeferredNowMs;
        var order = ++_deferredOrder;

        _deferred.Enqueue(work, (now + Math.Max(0, delayMs), order));
    }

    private async Task RunDueDeferredAsync(long now, CancellationToken ct)
    {
        _lastTickMs = now;

        while (_deferred.TryPeek(out var work, out var due) && due.DueAtMs <= now)
        {
            _deferred.Dequeue();

            try
            {
                await work(ct);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(
                    ex,
                    "Deferred wired work failed in room {RoomId}",
                    _roomGrain.RoomId
                );
            }
        }
    }
}
