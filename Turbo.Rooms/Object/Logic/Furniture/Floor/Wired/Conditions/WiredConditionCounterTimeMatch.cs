using System.Collections.Generic;
using System.Linq;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Counters;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Conditions;

/// <summary>
/// Compares the picked counter clocks to a time. Params: seconds, minutes, half-second
/// remainder and the three-way comparison.
/// </summary>
[RoomObjectLogic("wf_cnd_counter_time_matches")]
public class WiredConditionCounterTimeMatch(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredConditionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredConditionType.CLOCK_TIME_MATCHES;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, 59, 0),
            new WiredRangeParamRule(0, 99, 0),
            new WiredRangeParamRule(0, 1, 0),
            new WiredRangeParamRule(0, 2, 1),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    protected override bool EvaluateCore(IWiredProcessingContext ctx)
    {
        var target =
            (GetIntParamOrDefault(1, 0) * 60 + GetIntParamOrDefault(0, 0)) * 2
            + GetIntParamOrDefault(2, 0);
        var comparison = GetIntParamOrDefault(3, 1);
        var clocks = GetFloorItems(ctx.GetSelection(this))
            .Select(x => x.Logic as FurnitureCounterClockLogic)
            .Where(x => x is not null)
            .ToList();

        return Quantify(
            clocks.Select(c => WiredComparison.CompareThreeWay(comparison, c!.HalfSeconds, target)),
            false
        );
    }
}
