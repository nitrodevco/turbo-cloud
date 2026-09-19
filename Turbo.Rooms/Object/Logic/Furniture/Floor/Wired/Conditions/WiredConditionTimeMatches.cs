using System;
using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True when the wall clock time matches. Nine params: three "use" flags for seconds, minutes
/// and hours, then a min and max for each. The string param names a timezone; empty uses the
/// room wired timezone.
/// </summary>
[RoomObjectLogic("wf_cnd_match_time")]
public class WiredConditionTimeMatches(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.TIME_MATCHES;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredBoolParamRule(false), // use seconds
            new WiredBoolParamRule(false), // use minutes
            new WiredBoolParamRule(false), // use hours
            new WiredRangeParamRule(0, 59, 0),
            new WiredRangeParamRule(0, 59, 59),
            new WiredRangeParamRule(0, 59, 0),
            new WiredRangeParamRule(0, 59, 59),
            new WiredRangeParamRule(0, 23, 0),
            new WiredRangeParamRule(0, 23, 23),
        ];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var now = WiredTimeZones.GetLocalTime(_roomGrain, _wiredData.StringParam);

        if (GetIntParamOrDefault(0, false) && !InRange(now.Second, 3))
            return false;

        if (GetIntParamOrDefault(1, false) && !InRange(now.Minute, 5))
            return false;

        if (GetIntParamOrDefault(2, false) && !InRange(now.Hour, 7))
            return false;

        return true;
    }

    private bool InRange(int value, int minIndex)
    {
        var min = GetIntParamOrDefault(minIndex, 0);
        var max = GetIntParamOrDefault(minIndex + 1, 0);

        return value >= Math.Min(min, max) && value <= Math.Max(min, max);
    }
}
