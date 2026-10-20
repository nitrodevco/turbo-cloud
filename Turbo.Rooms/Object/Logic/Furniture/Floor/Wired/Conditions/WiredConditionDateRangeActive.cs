using System;
using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// True between two unix timestamps (params 0 and 1, seconds). The client omits a bound it
/// could not parse, and zero is no bound: a missing or zero start means "since forever", a
/// missing or zero end "until forever". A fresh box holds both at zero, like the official one
/// (defaults [0,0], 2026-10-20T17-37-19-Open-wait).
/// </summary>
[RoomObjectLogic("wf_cnd_date_rng_active")]
public class WiredConditionDateRangeActive(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.DATE_RANGE_ACTIVE;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [WiredRules.NonNegative(), WiredRules.NonNegative()];

    protected override int GetRequiredIntParamCount() => 0;

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var start = GetIntParamOrDefault(0, 0);
        var end = GetIntParamOrDefault(1, 0);

        return now >= start && (end <= 0 || now <= end);
    }
}
