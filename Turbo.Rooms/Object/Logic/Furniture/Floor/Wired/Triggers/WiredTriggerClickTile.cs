using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Triggers;

/// <summary>
/// Fires when a player clicks one of the invisible click tiles it picked, which is then the
/// triggering item. It picks nothing else: the hotel's own refusal for anything else is
/// <c>wiredfurni.error.require_click_tiles</c> ("Can only select invisible click furniture.").
/// </summary>
[RoomObjectLogic("wf_trg_click_tile")]
public class WiredTriggerClickTile(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
)
    : FurnitureWiredFurniEventTriggerLogic<PlayerClickedTileEvent>(
        grainFactory,
        stuffDataFactory,
        ctx
    )
{
    public override int WiredCode => (int)WiredTriggerType.USER_CLICKS_TILE;

    protected override RoomObjectId GetFurniId(PlayerClickedTileEvent evt) => evt.FurniId;

    protected override bool CanPickFurni(IRoomItem item) =>
        item.Logic is FurnitureInvisibleClickTileLogic;

    protected override string PickRefusedErrorKey => WiredSaveErrors.REQUIRE_CLICK_TILES;
}
