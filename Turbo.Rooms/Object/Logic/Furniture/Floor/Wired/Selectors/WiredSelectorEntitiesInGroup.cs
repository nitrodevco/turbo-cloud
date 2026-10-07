using System.Threading;
using System.Threading.Tasks;
using Orleans;
using Turbo.Primitives.Furniture.Providers;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Object.Furniture.Floor;
using Turbo.Primitives.Rooms.Object.Logic;
using Turbo.Primitives.Rooms.Wired;
using Turbo.Rooms.Object.Avatars.Player;
using Turbo.Rooms.Wired;

namespace Turbo.Rooms.Object.Logic.Furniture.Floor.Wired.Selectors;

/// <summary>
/// Picks the players wearing a group's badge: the room's own group when the string param is
/// empty (the editor's "Current group", <c>wiredfurni.params.grouptype.0</c>), otherwise the group
/// with that id. A room without a group has no current group, and nobody is picked.
/// </summary>
[RoomObjectLogic("wf_slc_users_group")]
public class WiredSelectorEntitiesInGroup(
    IGrainFactory grainFactory,
    IStuffDataFactory stuffDataFactory,
    IRoomFloorItemContext ctx
) : FurnitureWiredSelectorLogic(grainFactory, stuffDataFactory, ctx)
{
    public override int WiredCode => (int)WiredSelectorType.USERS_IN_GROUP;

    public override Task<IWiredSelectionSet> SelectAsync(
        IWiredProcessingContext ctx,
        CancellationToken ct
    )
    {
        var output = new WiredSelectionSet();
        var wantedGroupId =
            GetPositiveIdParam() ?? _roomGrain._state.RoomSnapshot?.Guild?.GuildId.Value;

        if (wantedGroupId is not > 0)
            return Task.FromResult<IWiredSelectionSet>(output);

        foreach (var avatar in AvatarModule.Avatars)
        {
            if (avatar is not RoomPlayerAvatar player || player.GuildId <= 0)
                continue;

            if (player.GuildId == wantedGroupId)
                output.SelectedAvatarIds.Add(player.ObjectId);
        }

        return Task.FromResult<IWiredSelectionSet>(output);
    }
}
