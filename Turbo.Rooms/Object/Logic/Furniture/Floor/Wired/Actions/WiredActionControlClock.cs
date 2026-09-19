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

/// <summary>Starts, stops, resets, restarts or toggles the picked counter clocks.</summary>
[RoomObjectLogic("wf_act_control_clock")]
public class WiredActionControlClock(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredActionLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredActionType.CONTROL_CLOCK;

    public override List<IWiredParamRule> GetIntParamRules() =>
        [new WiredEnumParamRule<WiredClockControlType>(WiredClockControlType.Start)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [WiredSources.PickedFurni];

    public override async Task<bool> ExecuteAsync(IWiredExecutionContext ctx, CancellationToken ct)
    {
        var control = GetIntParamOrDefault(0, WiredClockControlType.Start);
        var controlled = false;

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            if (item.Logic is not FurnitureCounterClockLogic clock)
                continue;

            await clock.ControlAsync(control, ct);

            controlled = true;
        }

        return controlled;
    }
}
