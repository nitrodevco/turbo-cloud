using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.RoomItem;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

[RoomObjectLogic("wf_trg_click_furni")]
public class WiredTriggerClickFurni(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.AVATAR_CLICKS_FURNI;
    public override List<Type> SupportedEventTypes { get; } = [typeof(RoomItemClickedEvent)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems],
        ];

    public override async Task<bool> CanTriggerAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.Event is not RoomItemClickedEvent evt)
            return false;

        var selection = await ctx.GetEffectiveSelectionAsync(this, ct);

        if (!selection.SelectedFurniIds.Contains((int)evt.ObjectId))
            return false;

        return true;
    }
}
