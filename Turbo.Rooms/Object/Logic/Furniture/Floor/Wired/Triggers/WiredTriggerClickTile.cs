using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

[RoomObjectLogic("wf_trg_click_tile")]
public class WiredTriggerClickTile(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.USER_CLICKS_TILE;
    public override List<Type> SupportedEventTypes { get; } = [typeof(PlayerClickedTileEvent)];

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (ctx.Event is not PlayerClickedTileEvent evt)
            return Task.FromResult(false);

        return Task.FromResult(true);
    }
}
