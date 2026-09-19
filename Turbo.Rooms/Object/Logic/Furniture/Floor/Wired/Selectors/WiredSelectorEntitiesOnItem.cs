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
            foreach (var avatar in _roomGrain.AvatarModule.GetAvatarsOnItem(item))
            {
                if (avatar is IRoomPlayer player)
                    output.SelectedPlayerIds.Add(player.PlayerId);
            }
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
