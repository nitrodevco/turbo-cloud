using System;
using System.Collections.Generic;
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
    IWiredDataFactory wiredDataFactory,
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredTriggerLogic(wiredDataFactory, grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredTriggerType.AVATAR_CLICKS_FURNI;
    public override List<Type> SupportedEventTypes { get; } = [typeof(RoomItemClickedEvent)];

    private WiredSourceType _itemSource;

    public override List<WiredSourceType[]> GetFurniSources() =>
        [
            [WiredSourceType.SELECTED_ITEMS, WiredSourceType.SELECTOR_ITEMS],
        ];

    public override Task<bool> MatchesAsync(IWiredContext ctx)
    {
        var result = ctx.Event is RoomItemClickedEvent;

        return Task.FromResult(result);
    }

    protected override void FillInternalData()
    {
        base.FillInternalData();

        _itemSource = WiredData.FurniSources.GetValueOrDefault(0, WiredSourceType.SELECTED_ITEMS);
    }
}
