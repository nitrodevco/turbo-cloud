using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;
using Turbo.Rooms.Wired.Rules;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Actions;

/// <summary>
/// Sets, adds or subtracts time on the picked counter clocks. Params: seconds, minutes, the
/// half-second remainder and the operator.
/// </summary>
[RoomObjectLogic("wf_act_adjust_clock")]
public class WiredActionAdjustClock(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.ADJUST_CLOCK;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [
            new WiredRangeParamRule(0, 59, 0),
            new WiredRangeParamRule(0, 99, 0),
            new WiredRangeParamRule(0, 1, 0),
            new WiredEnumParamRule<WiredOperatorType>(WiredOperatorType.Set),
        ];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var halfSeconds =
            (GetIntParamOrDefault(1, 0) * 60 + GetIntParamOrDefault(0, 0)) * 2
            + GetIntParamOrDefault(2, 0);
        var op = GetIntParamOrDefault(3, WiredOperatorType.Set);
        var adjusted = false;

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            if (item.Logic is not FurnitureCounterClockLogic clock)
                continue;

            await clock.AdjustAsync(op, halfSeconds, ct);

            adjusted = true;
        }

        return adjusted;
    }
}
