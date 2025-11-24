using System.Threading;
using System.Threading.Tasks;
using Turbo.Contracts.Enums.Rooms;
using Turbo.Primitives.Actor;
using Turbo.Primitives.Rooms;

namespace Turbo.Rooms.Grains.Modules;

internal sealed partial class RoomActionModule(
    RoomGrain roomGrain,
    RoomLiveState liveState,
    RoomFurniModule furniModule
) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomLiveState _state = liveState;
    private readonly RoomFurniModule _furniModule = furniModule;

    public async Task OnActivateAsync(CancellationToken ct) { }

    public async Task OnDeactivateAsync(CancellationToken ct) { }

    private async Task<bool> CanManipulateFurniAsync(ActorContext ctx)
    {
        var controllerLevel = await GetControllerLevelAsync(ctx);

        if (controllerLevel >= RoomControllerLevel.GroupAdmin)
            return true;

        var isGroupRoom = false;

        if (isGroupRoom)
        {
            var canGroupDecorate = false;

            if (controllerLevel >= RoomControllerLevel.GroupRights && canGroupDecorate)
                return true;
        }
        else
        {
            if (controllerLevel >= RoomControllerLevel.Rights)
                return true;
        }

        return false;
    }

    public async Task<bool> GetIsRoomOwnerAsync(ActorContext ctx)
    {
        if (ctx is null)
            return false;

        if (_state.RoomSnapshot.OwnerId == ctx.PlayerId)
            return true;

        // if has perm any_room_owner true

        return false;
    }

    public async Task<RoomControllerLevel> GetControllerLevelAsync(ActorContext ctx)
    {
        if (ctx.Origin == ActorOrigin.System)
            return RoomControllerLevel.Moderator;

        if (await GetIsRoomOwnerAsync(ctx))
            return RoomControllerLevel.Owner;

        var isGroupRoom = false;

        if (isGroupRoom)
        {
            // if has perm group_admin GroupAdmin
            // if has perm group_member GroupMember

            // check if belongs to group
        }
        else
        {
            // if has perm room_rights Rights

            if (_state.PlayerIdsWithRights.Contains(ctx.PlayerId))
                return RoomControllerLevel.Rights;
        }

        return RoomControllerLevel.None;
    }
}
