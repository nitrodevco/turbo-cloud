using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>Picks the players standing on the furni its inputs resolve to.</summary>
[RoomObjectLogic("wf_slc_users_onfurni")]
public class WiredSelectorEntitiesOnItem(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_ON_FURNI;

    public override bool SelectsFurni => false;

    public override bool SelectsUsers => true;

    // Picked, triggering or signal furni; a selector does not read the selector pool (official
    // form contract of Users On Furni, [0,100,201], default 100).
    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() =>
        [
            [
                WiredFurniSourceType.SelectedItems,
                WiredFurniSourceType.TriggeredItem,
                WiredFurniSourceType.SignalItems,
            ],
        ];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            if (!FurniModule.GetTileIdForFloorItem(item, out var tileIds))
                continue;

            foreach (var tileIdx in InTileOrder(tileIds))
            {
                foreach (var avatar in AvatarModule.GetAvatarsOnTile(tileIdx))
                    output.SelectedAvatarIds.Add(avatar.ObjectId);
            }
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }

    /// <summary>
    /// The users come in the order of the furni's tiles they stand on, not by room index, entry or
    /// name: on a 2x2 at 5,9 that is (5,10), (5,9), (6,9), (6,10) (official client capture
    /// 2026-10-24, wired matrix selector.8.R4, 5 layouts of a user and two bots, 10/10 firings).
    /// Inference: the walk goes column by column from the left, the first down from the far end
    /// and the next back up; one 2x2 furni was measured.
    /// </summary>
    public override WiredUserOrderType UserOrder => WiredUserOrderType.AsSelected;

    private IEnumerable<int> InTileOrder(List<int> tileIds) =>
        tileIds
            .Select(MapModule.GetTileXY)
            .GroupBy(tile => tile.x)
            .OrderBy(column => column.Key)
            .SelectMany(
                (column, index) =>
                    index % 2 == 0
                        ? column.OrderByDescending(tile => tile.y)
                        : column.OrderBy(tile => tile.y)
            )
            .Select(tile => MapModule.ToIdx(tile.x, tile.y));
}
