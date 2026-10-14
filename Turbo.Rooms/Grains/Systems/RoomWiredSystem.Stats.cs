using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    // The room's wired usage: what recent executions cost, falling by a factor every step.
    private double _usage;
    private long _usageStepAtMs;

    // Server time the room's wired executions took in the current heavy window.
    private long _busyTicksInWindow;
    private long _heavyWindowStartMs;

    // Until when a KILLED room runs no wired.
    private long _killedUntilMs;

    /// <summary>
    /// The room's wired usage as the monitor shows it: what recent executions cost, decayed to
    /// <paramref name="now"/>.
    /// </summary>
    public double GetExecutionCost(long now)
    {
        DecayUsage(now);

        return _usage;
    }

    /// <summary>
    /// Whether the room is marked heavy: its wired took more server time than the configured share
    /// of the last window. Apart from the usage cap.
    /// </summary>
    public bool IsHeavy { get; private set; }

    /// <summary>Whether wired in the room is halted after a KILLED.</summary>
    public bool IsKilled(long now) => now < _killedUntilMs;

    /// <summary>
    /// Adds a cost to the room's usage for an execution. When that takes the usage past the cap,
    /// the execution is cancelled and EXECUTION_CAP counted once for it; false then. The cost is
    /// added before the check, so the usage can end a little past the cap (official client
    /// capture: 4687.807 read against a cap of 4687.5).
    /// </summary>
    private bool ChargeExecution(WiredExecution execution, double cost)
    {
        if (execution.IsStopped)
            return false;

        DecayUsage(execution.Now);

        _usage += Math.Max(0, cost);

        var cap = _roomGrain._wiredConfig.ExecutionCostCap;

        if (cap <= 0 || _usage <= cap)
            return true;

        if (execution.Stop())
            RecordError(WiredErrorKind.ExecutionCap, execution.Now);

        return false;
    }

    private void DecayUsage(long now)
    {
        var stepMs = _roomGrain._wiredConfig.ExecutionCostDecayMs;

        if (_usageStepAtMs == 0 || stepMs <= 0)
        {
            _usageStepAtMs = now;

            return;
        }

        var steps = (now - _usageStepAtMs) / stepMs;

        if (steps <= 0)
            return;

        _usage *= Math.Pow(_roomGrain._wiredConfig.ExecutionCostDecayFactor, steps);
        _usageStepAtMs += steps * stepMs;
    }

    /// <summary>
    /// Once a heavy window is over, marks the room heavy or not by the time its executions took
    /// in it. Becoming heavy counts MARKED_AS_HEAVY.
    /// </summary>
    private void RollHeavyWindow(long now)
    {
        var windowMs = Math.Max(1, _roomGrain._wiredConfig.HeavyWindowMs);

        if (_heavyWindowStartMs == 0)
        {
            _heavyWindowStartMs = now;

            return;
        }

        if (now - _heavyWindowStartMs < windowMs)
            return;

        var threshold = _roomGrain._wiredConfig.HeavyBusyMsPerWindow;
        var busyMs = _busyTicksInWindow * 1000.0 / Stopwatch.Frequency;
        var wasHeavy = IsHeavy;

        IsHeavy = threshold > 0 && busyMs > threshold;

        if (IsHeavy && !wasHeavy)
            RecordError(WiredErrorKind.MarkedAsHeavy, now);

        _busyTicksInWindow = 0;
        _heavyWindowStartMs = now;
    }

    /// <summary>
    /// After an execution: one that ran past <c>WiredConfig.KillExecutionMs</c> halts the room's
    /// wired for <c>WiredConfig.KilledDurationMs</c>, dropping what waited to run, and counts KILLED.
    /// </summary>
    private void KillIfOverrun(WiredExecution execution)
    {
        var limit = _roomGrain._wiredConfig.KillExecutionMs;

        if (limit <= 0 || _wallClockMs() - execution.StartedAtWallMs <= limit)
            return;

        _killedUntilMs = execution.Now + _roomGrain._wiredConfig.KilledDurationMs;

        _eventQueue.Clear();
        _pendingStackExecutions.Clear();
        _stackSchedule.Clear();

        RecordError(WiredErrorKind.Killed, execution.Now);

        _roomGrain._logger.LogWarning(
            "Wired in room {RoomId} is halted for {Ms} ms: one execution ran past {Limit} ms",
            _roomGrain.RoomId,
            _roomGrain._wiredConfig.KilledDurationMs,
            limit
        );
    }

    /// <summary>
    /// True when the room already holds the configured maximum of permanent variables for the
    /// given target type.
    /// </summary>
    public async Task<bool> IsPermanentVariableCapReachedAsync(
        WiredVariableTargetType targetType,
        CancellationToken ct
    )
    {
        var cap = targetType switch
        {
            WiredVariableTargetType.Furni => _roomGrain._wiredConfig.MaxPermanentFurniVariables,
            WiredVariableTargetType.User => _roomGrain._wiredConfig.MaxPermanentUserVariables,
            WiredVariableTargetType.Global => _roomGrain._wiredConfig.MaxPermanentGlobalVariables,
            _ => 0,
        };

        if (cap <= 0)
            return false;

        var variables = await GetWiredVariablesSnapshotAsync(ct);

        return CountPermanentVariables(variables.Variables, targetType) >= cap;
    }

    public static int CountPermanentVariables(
        IEnumerable<WiredVariableSnapshot> variables,
        WiredVariableTargetType targetType
    ) => variables.Count(x => x.AvailabilityType.IsPermanent() && x.TargetType == targetType);

    /// <summary>
    /// Every furni in the room, wired or not and wired-placed ones included, split by floor and
    /// wall: what Habbo's monitor counts against its floor and wall caps (official client
    /// capture: 33 floor and 4 wall furni in a room, 168 floor furni with temporary ones).
    /// </summary>
    public (int floor, int wall) CountRoomItems()
    {
        var floor = 0;
        var wall = 0;

        foreach (var item in FurniModule.Items)
        {
            if (item is IRoomWallItem)
                wall++;
            else if (item is IRoomFloorItem)
                floor++;
        }

        return (floor, wall);
    }
}
