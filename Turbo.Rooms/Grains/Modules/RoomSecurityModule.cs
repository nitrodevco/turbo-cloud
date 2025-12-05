using System.Threading.Tasks;
using Turbo.Primitives.Action;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Grains.Modules;

internal sealed class RoomSecurityModule(RoomGrain roomGrain, RoomLiveState liveState) : IRoomModule
{
    private readonly RoomGrain _roomGrain = roomGrain;
    private readonly RoomLiveState _state = liveState;

    public async Task<bool> CanManipulateFurniAsync(ActionContext ctx)
    {
        var controllerLevel = await GetControllerLevelAsync(ctx);

        if (controllerLevel >= RoomControllerType.GroupAdmin)
            return true;

        var isGroupRoom = false;

        if (isGroupRoom)
        {
            var canGroupDecorate = false;

            if (controllerLevel >= RoomControllerType.GroupRights && canGroupDecorate)
                return true;
        }
        else
        {
            if (controllerLevel >= RoomControllerType.Rights)
                return true;
        }
        return false;
    }

    public async Task<bool> CanUseFurniAsync(ActionContext ctx, FurnitureUsageType usageType)
    {
        var controllerLevel = await GetControllerLevelAsync(ctx);

        if (usageType == FurnitureUsageType.Nobody)
            return false;

        if (usageType == FurnitureUsageType.Controller)
        {
            if (controllerLevel < RoomControllerType.Rights)
                return false;
        }

        return true;
    }

    public async Task<bool> CanPlaceFurniAsync(ActionContext ctx)
    {
        // TODO placement rules?

        return await CanManipulateFurniAsync(ctx);
    }

    public Task<bool> GetIsRoomOwnerAsync(ActionContext ctx)
    {
        var isOwner = false;

        if (ctx is not null && _state.RoomSnapshot.OwnerId == ctx.PlayerId)
            isOwner = true;

        // if has perm any_room_owner true

        return Task.FromResult(isOwner);
    }

    public async Task<RoomControllerType> GetControllerLevelAsync(ActionContext ctx)
    {
        if (ctx.Origin == ActionOrigin.System)
            return RoomControllerType.Moderator;

        if (await GetIsRoomOwnerAsync(ctx))
            return RoomControllerType.Owner;

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
                return RoomControllerType.Rights;
        }

        return RoomControllerType.None;
    }
}
