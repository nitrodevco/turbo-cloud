using System.Collections.Generic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Wired;

/// <summary>
/// One wired execution: what an event, a timer or a delayed continuation starts. Every signal
/// and stack call it makes runs inside it, in the same tick, rather than as a new event on the
/// next one; a delay ends it, and what runs after the delay is a new execution with fresh
/// budgets (official client capture: a ring of 24 stacks made 2400 visits in one event, and
/// ActionDelay 1 starts a new execution).
/// </summary>
internal sealed class WiredExecution(long now, long startedAtWallMs)
{
    private readonly Dictionary<int, int> _visitsByStackId = [];

    /// <summary>The tick time the execution runs at.</summary>
    public long Now { get; } = now;

    /// <summary>When it started on the wall clock, for the time limits.</summary>
    public long StartedAtWallMs { get; } = startedAtWallMs;

    /// <summary>The action chains chosen so far and not run yet, in the order they were chosen.</summary>
    public Queue<(WiredExecutionKey Key, WiredPendingStackExecution Pending)> Ready { get; } =
        new();

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

    /// <summary>
    /// Counts a visit to a stack; false once the stack had <paramref name="limit"/> visits in this
    /// execution (0 or less is no limit). The visit past the limit is dropped without an error
    /// (official client capture: two-stack ping-pong rings stopped at 200 visits, 100 each).
    /// </summary>
    public bool TryVisit(int stackId, int limit)
    {
        _visitsByStackId.TryGetValue(stackId, out var visits);

        if (limit > 0 && visits >= limit)
            return false;

        _visitsByStackId[stackId] = visits + 1;

        return true;
    }
}
