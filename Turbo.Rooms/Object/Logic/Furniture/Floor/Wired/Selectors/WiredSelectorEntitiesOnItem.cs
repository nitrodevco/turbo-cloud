using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Avatars;
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

    public override List<WiredFurniSourceType[]> GetAllowedFurniSources() => [WiredSources.Furni];

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();

        foreach (var item in GetFloorItems(ctx.GetSelection(this)))
        {
            if (!_roomGrain.FurniModule.GetTileIdForFloorItem(item, out var tileIds))
                continue;

            foreach (var tileId in tileIds)
            {
                if (!_roomGrain.MapModule.InBounds(tileId))
                    continue;

                foreach (var avatarId in _roomGrain._state.TileAvatarStacks[tileId])
                {
                    if (
                        _roomGrain._state.AvatarsByObjectId.TryGetValue(avatarId, out var avatar)
                        && avatar is IRoomPlayer player
                    )
                        output.SelectedPlayerIds.Add(player.PlayerId);
                }
            }
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
