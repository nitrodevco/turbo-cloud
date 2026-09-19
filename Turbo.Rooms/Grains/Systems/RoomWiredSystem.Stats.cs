using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Furniture.Wall;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Grains.Systems;

public sealed partial class RoomWiredSystem
{
    private long _executionWindowStartMs;
    private int _executionsInWindow;
    private int _executionsLastWindow;

    /// <summary>
    /// Actions executed during the last completed cost window. Used as the room's wired
    /// execution cost: a room that constantly runs near the cap is "heavy".
    /// </summary>
    public int GetExecutionCost(long now)
    {
        RollExecutionWindow(now);

        return _executionsLastWindow;
    }

    private void CountExecution() => _executionsInWindow++;

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
    ) =>
        variables.Count(x =>
            x.AvailabilityType == WiredAvailabilityType.Persistent && x.TargetType == targetType
        );

    /// <summary>
    /// Wired boxes currently in the room, split by floor and wall placement.
    /// </summary>
    public (int floor, int wall) CountWiredItems()
    {
        var floor = 0;
        var wall = 0;

        foreach (var item in _roomGrain.FurniModule.Items)
        {
            if (item.Logic is not IWiredBox)
                continue;

            if (item is IRoomWallItem)
                wall++;
            else if (item is IRoomFloorItem)
                floor++;
        }

        return (floor, wall);
    }

    private void RollExecutionWindow(long now)
    {
        var windowMs = _roomGrain._wiredConfig.ExecutionCostWindowMs;

        if (_executionWindowStartMs == 0)
        {
            _executionWindowStartMs = now;

            return;
        }

        var elapsed = now - _executionWindowStartMs;

        if (elapsed < windowMs)
            return;

        // More than one window passed without a tick: the room was idle, so the last full
        // window carried no executions.
        _executionsLastWindow = elapsed < windowMs * 2 ? _executionsInWindow : 0;
        _executionsInWindow = 0;
        _executionWindowStartMs = now;
    }
}
