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
/// could not parse, so a missing start means "since forever" and a missing end "until forever".
/// </summary>
[RoomObjectLogic("wf_cnd_date_rng_active")]
public class WiredConditionDateRangeActive(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.DATE_RANGE_ACTIVE;

    public override List<IWiredParamRule> GetIntParamRules() => [];

    public override IWiredParamRule? GetIntParamTailRule() => new WiredParamRule(0);

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var count = _wiredData.IntParams.Count;

        if (count == 0)
            return false;

        var start = GetIntParamOrDefault(0, 0);

        if (now < start)
            return false;

        if (count < 2)
            return true;

        var end = GetIntParamOrDefault(1, 0);

        return end <= 0 || now <= end;
    }
}
