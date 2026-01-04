using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

[RoomObjectLogic("wf_trg_walks_on_furni")]
public class WiredTriggerWalkOnFurni(
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.AVATAR_WALKS_ON_FURNI;
    public override List<Type> SupportedEventTypes => [typeof(AvatarWalkOnFurniEvent)];

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [WiredFurniSourceType.SelectedItems, WiredFurniSourceType.SelectorItems],
        ];

    public override async Task<bool> CanTriggerAsync(
        WiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        if (ctx.Event is not AvatarWalkOnFurniEvent evt)
            return false;

        var selection = await ctx.GetEffectiveSelectionAsync(this, ct);

        if (!selection.SelectedFurniIds.Contains(evt.FurniId))
            return false;

        return true;
    }
}
