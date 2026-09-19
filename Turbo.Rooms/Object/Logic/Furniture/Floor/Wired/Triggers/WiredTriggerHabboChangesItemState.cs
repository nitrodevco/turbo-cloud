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

/// <summary>Fires when a player uses (double-clicks) one of the picked furni.</summary>
[RoomObjectLogic("wf_trg_stuff_state")]
public class WiredTriggerHabboChangesItemState(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.USE_STUFF;
    public override List<Type> SupportedEventTypes { get; } = [typeof(RoomItemUsedEvent)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems],
        ];

    public override Task<bool> CanTriggerAsync(IWiredProcessingContext ctx, CancellationToken ct)
    {
        if (ctx.Event is not RoomItemUsedEvent used)
            return Task.FromResult(false);

        var selection = ctx.GetSelection(this);

        return Task.FromResult(selection.SelectedFurniIds.Contains(used.ObjectId));
    }
}
