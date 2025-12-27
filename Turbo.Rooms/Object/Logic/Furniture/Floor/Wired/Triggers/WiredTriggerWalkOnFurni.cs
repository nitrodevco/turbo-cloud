using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Avatar;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

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
    public override List<Type> SupportedEventTypes { get; } = [typeof(AvatarWalkOnFurniEvent)];

    public override List<WiredSourceType[]> GetFurniSources() =>
        [
            [WiredSourceType.SELECTED_ITEMS, WiredSourceType.SELECTOR_ITEMS],
        ];

    public override Task<bool> MatchesAsync(IWiredContext ctx)
    {
        var result = ctx.Event is AvatarWalkOnFurniEvent;

        return Task.FromResult(result);
    }
}
