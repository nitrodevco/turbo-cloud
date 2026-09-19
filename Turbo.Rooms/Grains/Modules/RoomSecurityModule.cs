using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Settings;

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

        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync(CancellationToken.None);

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

    public Task<bool> GetIsRoomOwnerAsync(ActionContext ctx) => GetIsRoomOwnerAsync(ctx.PlayerId);

    public Task<bool> GetIsRoomOwnerAsync(PlayerId playerId)
    {
        var isOwner = false;

        if (_roomGrain._state.RoomSnapshot.OwnerId == playerId)
            isOwner = true;

        // if has perm any_room_owner true

        return Task.FromResult(isOwner);
    }

    /// <summary>
    /// System-originated actions act as a moderator; everything else resolves by player id.
    /// </summary>
    public Task<RoomControllerType> GetControllerLevelAsync(ActionContext ctx) =>
        ctx.Origin == ActionOrigin.System
            ? Task.FromResult(RoomControllerType.Moderator)
            : GetControllerLevelAsync(ctx.PlayerId);

    public async Task<RoomControllerType> GetControllerLevelAsync(PlayerId playerId)
    {
        if (await GetIsRoomOwnerAsync(playerId))
            return RoomControllerType.Owner;

        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync(CancellationToken.None);

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

    /// <summary>
    /// Resolves the room's wired permission masks against a controller level. The owner and
    /// staff always pass; group admins count as group members.
    /// </summary>
    public (bool canModify, bool canRead) GetWiredPermissions(RoomControllerType controllerLevel)
    {
        var snapshot = _roomGrain._state.RoomSnapshot;

        return (
            IsWiredPermitted(snapshot.WiredModifyPermissionMask, controllerLevel),
            IsWiredPermitted(snapshot.WiredReadPermissionMask, controllerLevel)
        );
    }

    private static bool IsWiredPermitted(WiredPermissionFlags mask, RoomControllerType level) =>
        level switch
        {
            >= RoomControllerType.Owner => true,
            RoomControllerType.GroupAdmin => mask.HasFlag(WiredPermissionFlags.GroupAdmins)
                || mask.HasFlag(WiredPermissionFlags.GroupMembers)
                || mask.HasFlag(WiredPermissionFlags.Everyone),
            RoomControllerType.GroupRights => mask.HasFlag(WiredPermissionFlags.GroupMembers)
                || mask.HasFlag(WiredPermissionFlags.Everyone),
            RoomControllerType.Rights => mask.HasFlag(WiredPermissionFlags.Rights)
                || mask.HasFlag(WiredPermissionFlags.Everyone),
            _ => mask.HasFlag(WiredPermissionFlags.Everyone),
        };

    /// <summary>
    /// Re-sends wired read/modify permissions to everyone in the room, for when the masks change.
    /// </summary>
    public async Task RefreshWiredPermissionsForRoomAsync(CancellationToken ct)
    {
        var updates = new List<Task>();

        foreach (var playerId in _roomGrain._state.AvatarsByPlayerId.Keys.ToList())
        {
            var (canModify, canRead) = GetWiredPermissions(await GetControllerLevelAsync(playerId));

            updates.Add(
                _roomGrain
                    ._grainFactory.GetPlayerPresenceGrain(playerId)
                    .OnWiredPermissionsUpdatedAsync(_roomGrain.RoomId, canModify, canRead, ct)
            );
        }

        // One presence grain per player, so the updates do not wait on each other.
        await Task.WhenAll(updates);
    }

    public async Task RefreshControllerLevelForPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        var controllerLevel = await GetControllerLevelAsync(playerId);
        var (canModifyWired, canReadWired) = GetWiredPermissions(controllerLevel);
        var playerPresence = _roomGrain._grainFactory.GetPlayerPresenceGrain(playerId);

        await playerPresence
            .OnControllerLevelUpdatedAsync(
                _roomGrain.RoomId,
                controllerLevel,
                canModifyWired,
                canReadWired,
                ct
            )
            .ConfigureAwait(false);

        if (!_roomGrain.AvatarModule.TryGetPlayer(playerId, out var avatar))
            return;

        avatar.AddStatus(AvatarStatusType.FlatControl, ((int)controllerLevel).ToString());
    }

    /// <summary>A player with rights gives them up; the owner is told if they are online.</summary>
    public async Task<bool> RemoveOwnRightsAsync(PlayerId playerId, CancellationToken ct)
    {
        if (
            await _roomGrain.GetIsGroupRoomAsync(ct)
            || !_roomGrain._state.PlayerIdsWithRights.Contains(playerId)
        )
            return false;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .Set<RoomRightEntity>()
            .Where(x =>
                x.RoomEntityId == _roomGrain.RoomId.Value && x.PlayerEntityId == playerId.Value
            )
            .ExecuteDeleteAsync(ct);

        _roomGrain._state.PlayerIdsWithRights.Remove(playerId);

        await PublishRightsChangedAsync([playerId], ct);

        await RefreshControllerLevelForPlayerAsync(playerId, ct);

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            _roomGrain._state.RoomSnapshot.OwnerId,
            new FlatControllerRemovedEventMessageComposer
            {
                RoomId = _roomGrain.RoomId,
                PlayerId = playerId,
            },
            ct
        );

        return true;
    }

    private Task PublishRightsChangedAsync(IEnumerable<PlayerId> playerIds, CancellationToken ct) =>
        _roomGrain
            ._grainFactory.GetRoomDirectoryGrain()
            .PublishListingChangesAsync([.. playerIds.Select(NavigatorListingKeys.Rights)], ct);

    public async Task GiveRightsToPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync(ct);
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

        await PublishRightsChangedAsync([playerId], ct);

        await RefreshControllerLevelForPlayerAsync(playerId, ct);

        // Keeps the actor's open room settings rights list in sync.
        var name = await _roomGrain
            ._grainFactory.GetPlayerDirectoryGrain()
            .GetPlayerNameAsync(playerId, ct);

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new FlatControllerAddedEventMessageComposer
            {
                RoomId = _roomGrain.RoomId,
                Controller = new RoomControllerSnapshot { PlayerId = playerId, Name = name },
            },
            ct
        );
    }

    public async Task RemoveRightsFromPlayerAsync(
        ActionContext ctx,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync(ct);
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

        await PublishRightsChangedAsync([playerId], ct);

        await RefreshControllerLevelForPlayerAsync(playerId, ct);

        await _roomGrain._grainFactory.SendComposerToPlayerAsync(
            ctx.PlayerId,
            new FlatControllerRemovedEventMessageComposer
            {
                RoomId = _roomGrain.RoomId,
                PlayerId = playerId,
            },
            ct
        );
    }

    public async Task RemoveAllRightsAsync(ActionContext ctx, CancellationToken ct)
    {
        await EnsureRightsLoadedAsync(ct);

        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync(ct);

        if (!await GetIsRoomOwnerAsync(ctx) || isGroupRoom)
            return;

        var playerIds = _roomGrain._state.PlayerIdsWithRights.ToList();

        if (playerIds.Count == 0)
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        await dbCtx
            .Set<RoomRightEntity>()
            .Where(x => x.RoomEntityId == _roomGrain.RoomId.Value)
            .ExecuteDeleteAsync(ct);

        _roomGrain._state.PlayerIdsWithRights.Clear();

        await PublishRightsChangedAsync(playerIds, ct);

        foreach (var playerId in playerIds)
            await RefreshControllerLevelForPlayerAsync(playerId, ct);

        var removedComposers = playerIds
            .Select(playerId => new FlatControllerRemovedEventMessageComposer
            {
                RoomId = _roomGrain.RoomId,
                PlayerId = playerId,
            })
            .ToArray<IComposer>();

        await _roomGrain
            ._grainFactory.GetPlayerPresenceGrain(ctx.PlayerId)
            .SendComposerAsync(removedComposers, ct);
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
