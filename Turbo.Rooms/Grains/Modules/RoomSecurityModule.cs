using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Rooms.Grains.Modules;

public sealed class RoomSecurityModule(
    RoomGrain roomGrain,
    IDbContextFactory<TurboDbContext> dbCtxFactory
)
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;
    private readonly RoomGrain _roomGrain = roomGrain;

    public async Task<bool> CanManipulateFurniAsync(ActionContext ctx)
    {
        var controllerLevel = await GetControllerLevelAsync(ctx);

        if (controllerLevel >= RoomControllerType.GroupAdmin)
            return true;

        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync();

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

    public async Task<FurniturePickupType> GetFurniPickupTypeAsync(ActionContext ctx)
    {
        if (ctx.Origin == ActionOrigin.System)
            return FurniturePickupType.SendToOwner;

        // if can steal furni, SendToRequester

        if (await GetControllerLevelAsync(ctx) >= RoomControllerType.GroupAdmin)
            return FurniturePickupType.SendToOwner;

        return FurniturePickupType.None;
    }

    public Task<bool> GetIsRoomOwnerAsync(ActionContext ctx)
    {
        return GetIsRoomOwnerAsync(ctx.PlayerId);
    }

    public Task<bool> GetIsRoomOwnerAsync(PlayerId playerId)
    {
        var isOwner = false;

        if (_roomGrain._state.RoomSnapshot.OwnerId == playerId)
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

        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync();

        if (isGroupRoom)
        {
            // if has perm group_admin GroupAdmin
            // if has perm group_member GroupMember

            // check if belongs to group
        }
        else
        {
            // if has perm room_rights Rights

            if (_roomGrain._state.PlayerIdsWithRights.Contains(ctx.PlayerId))
                return RoomControllerType.Rights;
        }

        return RoomControllerType.None;
    }

    public async Task<RoomControllerType> GetControllerLevelAsync(PlayerId playerId)
    {
        if (await GetIsRoomOwnerAsync(playerId))
            return RoomControllerType.Owner;

        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync();

        if (isGroupRoom)
        {
            // if has perm group_admin GroupAdmin
            // if has perm group_member GroupMember

            // check if belongs to group
        }
        else
        {
            // if has perm room_rights Rights

            if (_roomGrain._state.PlayerIdsWithRights.Contains(playerId))
                return RoomControllerType.Rights;
        }

        return RoomControllerType.None;
    }

    public async Task RefreshControllerLevelForPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        var controllerLevel = await GetControllerLevelAsync(playerId);
        var playerPresence = _roomGrain._grainFactory.GetPlayerPresenceGrain(playerId);

        await playerPresence
            .OnControllerLevelUpdatedAsync(_roomGrain.RoomId, controllerLevel, ct)
            .ConfigureAwait(false);

        if (
            !_roomGrain._state.AvatarsByPlayerId.TryGetValue(playerId, out var objectId)
            || !_roomGrain._state.AvatarsByObjectId.TryGetValue(objectId, out var avatar)
        )
            return;

        avatar.AddStatus(AvatarStatusType.FlatControl, ((int)controllerLevel).ToString());
    }

    public async Task GiveRightsToPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync();
        var ctxIsOwner = await GetIsRoomOwnerAsync(ctx);
        var playerIsOwner = await GetIsRoomOwnerAsync(playerId);
        var playerControllerLevel = await GetControllerLevelAsync(playerId);

        if (
            !ctxIsOwner
            || playerIsOwner
            || playerControllerLevel >= RoomControllerType.Rights
            || isGroupRoom
        )
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entity = new RoomRightEntity
        {
            RoomEntityId = _roomGrain.RoomId.Value,
            PlayerEntityId = playerId.Value,
        };

        dbCtx.Attach(entity);

        await dbCtx.SaveChangesAsync(ct);

        _roomGrain._state.PlayerIdsWithRights.Add(playerId);

        await RefreshControllerLevelForPlayerAsync(playerId, ct);
    }

    public async Task RemoveRightsFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync();
        var ctxIsOwner = await GetIsRoomOwnerAsync(ctx);
        var playerIsOwner = await GetIsRoomOwnerAsync(playerId);
        var playerControllerLevel = await GetControllerLevelAsync(playerId);

        if (
            !ctxIsOwner
            || playerIsOwner
            || playerControllerLevel != RoomControllerType.Rights
            || isGroupRoom
        )
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .Set<RoomRightEntity>()
            .Where(x =>
                x.RoomEntityId == _roomGrain.RoomId.Value && x.PlayerEntityId == playerId.Value
            )
            .ExecuteDeleteAsync(ct);

        _roomGrain._state.PlayerIdsWithRights.Remove(playerId);

        await RefreshControllerLevelForPlayerAsync(playerId, ct);
    }

    internal async Task EnsureRightsLoadedAsync(CancellationToken ct)
    {
        if (_roomGrain._state.IsRightsLoaded)
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct).ConfigureAwait(false);

        var entities = await dbCtx
            .RoomRights.AsNoTracking()
            .Where(x => x.RoomEntityId == (int)_roomGrain.RoomId)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        foreach (var entity in entities)
            _roomGrain._state.PlayerIdsWithRights.Add(PlayerId.Parse(entity.PlayerEntityId));

        _roomGrain._state.IsRightsLoaded = true;
    }
}
