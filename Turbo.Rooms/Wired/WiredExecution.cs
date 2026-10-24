using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// One wired execution: what an event, a timer or a delayed continuation starts. Every signal
/// and stack call it makes runs inside it, in the same tick, rather than as a new event on the
/// next one; a delay ends it, and what runs after the delay is a new execution (official client
/// capture: a ring of 24 stacks made 2400 visits in one event, and receivers ran after the
/// sender stack's remaining effects).
/// </summary>
internal sealed class WiredExecution(long now, long startedAtWallMs)
{
    /// <summary>The tick time the execution runs at.</summary>
    public long Now { get; } = now;

    /// <summary>When it started on the wall clock, for the time limits.</summary>
    public long StartedAtWallMs { get; } = startedAtWallMs;

    /// <summary>The action chains chosen so far and not run yet, in the order they were chosen.</summary>
    public Queue<(WiredExecutionKey Key, WiredPendingStackExecution Pending)> Ready { get; } =
        new();

    /// <summary>The stacks that ran in it so far, however they were started.</summary>
    public HashSet<int> RanStackIds { get; } = [];

    /// <summary>Set when the execution was cancelled: nothing more of it runs.</summary>
    public bool IsStopped { get; private set; }

    /// <summary>Cancels the execution; true the first time, so its error is counted once.</summary>
    public bool Stop()
    {
        if (IsStopped)
            return false;

        IsStopped = true;

        return true;
    }
}
