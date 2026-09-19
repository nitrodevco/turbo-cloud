using System;
using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Primitives.Rooms.Wired.Variable;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// Compares how long ago the picked variable was created or last written on the selected
/// targets. Params: target, comparison (0 less, 2 more), which timestamp (0 creation,
/// 1 last update), duration (hi, lo), time unit.
/// </summary>
[RoomObjectLogic("wf_cnd_var_age_match")]
public class WiredConditionVariableAgeMatch(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.VARIABLE_AGE;

    public override int GetMaxVariableIds() => 1;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredParamRule((int)WiredVariableTargetType.User),
            new WiredRangeParamRule(0, 2, 2),
            new WiredBoolParamRule(false),
            new WiredParamRule(0),
            new WiredParamRule(0),
            new WiredEnumParamRule<WiredTimeUnitType>(WiredTimeUnitType.Seconds),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.SelectorItems,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    public override List<WiredPlayerSourceType[]> GetAllowedPlayerSources() =>
        [
            [
                WiredPlayerSourceType.TriggeredUser,
                WiredPlayerSourceType.SelectorUsers,
                WiredPlayerSourceType.SignalUsers,
            ],
        ];

    public override List<WiredVariableContextSnapshot> GetWiredContextSnapshots() =>
        [
            new WiredVariableAllInRoomSnapshot()
            {
                ContextType = WiredContextType.AllVariablesInRoom,
                AllVariablesHash = _roomGrain._state.AllVariablesHash,
            },
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var variable = GetVariable(0);

        if (variable is null)
            return false;

        var targetType = (WiredVariableTargetType)GetIntParamOrDefault(
            0,
            (int)variable.GetVarSnapshot().TargetType
        );
        var comparison = GetIntParamOrDefault(1, 2);
        var useUpdate = GetIntParamOrDefault(2, false);
        var durationMs = WiredTimeUnits.ToMilliseconds(
            GetLongParam(3),
            GetIntParamOrDefault(5, WiredTimeUnitType.Seconds)
        );
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var targets = GetTargetIds(targetType, ctx.GetSelection(this)).ToList();

        return Quantify(
            targets.Select(id =>
            {
                var key = new WiredVariableKey(
                    variable.GetVarSnapshot().VariableId,
                    targetType,
                    id
                );

                if (!variable.TryGetTimestamps(key, out var createdAt, out var updatedAt))
                    return false;

                var age = now - (useUpdate ? updatedAt : createdAt);

                return WiredComparison.CompareThreeWay(comparison, age, durationMs);
            }),
            true
        );
    }
}
