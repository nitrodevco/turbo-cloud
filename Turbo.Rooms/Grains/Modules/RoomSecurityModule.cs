using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Action;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Navigator;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Permissions;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Events.Player;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Primitives.Rooms.Object.Furniture;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Rooms.Grains.Modules;

public sealed class RoomSecurityModule(
    RoomGrain roomGrain,
    IDbContextFactory<TurboDbContext> dbCtxFactory
) : RoomGrainComponent(roomGrain)
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory = dbCtxFactory;

    public async Task<bool> CanManipulateFurniAsync(ActionContext ctx)
    {
        var controllerLevel = await GetControllerLevelAsync(ctx);

        if (controllerLevel >= RoomControllerType.GroupAdmin)
            return true;

        var isGroupRoom = await _roomGrain.GetIsGroupRoomAsync(CancellationToken.None);

        if (isGroupRoom)
        {
            // GroupRights is only ever handed out when the group says members may decorate, so
            // holding it is the permission; there is nothing further to ask.
            if (controllerLevel >= RoomControllerType.GroupRights)
                return true;
        }
        else
        {
            if (controllerLevel >= RoomControllerType.Rights)
                return true;
        }

        return false;
    }

    /// <summary>
    /// The usage type decides first: only a controller-only furni needs the player's level,
    /// which in a group homeroom is a question to the group.
    /// </summary>
    public async Task<bool> CanUseFurniAsync(ActionContext ctx, FurnitureUsageType usageType) =>
        usageType switch
        {
            FurnitureUsageType.Nobody => false,
            FurnitureUsageType.Controller => await GetControllerLevelAsync(ctx)
                >= RoomControllerType.Rights,
            _ => true,
        };

    /// <summary>
    /// Whether the player may bring furni into the room. Plain room rights (level 1, a room
    /// without a group) place only where the hotel says so
    /// (<see cref="Configuration.RoomConfig.RightsHoldersCanPlaceFurni"/>); moving, turning and
    /// using furni still follow <see cref="CanManipulateFurniAsync"/>, as on Habbo.
    /// </summary>
    public async Task<bool> CanPlaceFurniAsync(ActionContext ctx)
    {
        if (await GetControllerLevelAsync(ctx) == RoomControllerType.Rights)
            return _roomGrain._roomConfig.RightsHoldersCanPlaceFurni;

        return await CanManipulateFurniAsync(ctx);
    }

    public async Task<FurniturePickupType> GetFurniPickupTypeAsync(ActionContext ctx)
    {
        if (ctx.Origin == ActionOrigin.System)
            return FurniturePickupType.SendToOwner;

        if (await HasPermissionAsync(ctx.PlayerId, PermissionNodes.Room.FURNI_STEAL))
            return FurniturePickupType.SendToCtx;

        if (
            await GetControllerLevelAsync(ctx) >= RoomControllerType.GroupAdmin
            || await HasPermissionAsync(ctx.PlayerId, PermissionNodes.Room.FURNI_PICKUP_ANY)
        )
            return FurniturePickupType.SendToOwner;

        return FurniturePickupType.None;
    }

    /// <summary>
    /// Whether the player may pick up a wired variable box, which takes every value it stored
    /// with it: only the room's owner and the box's own may, whatever else they may pick up
    /// (Habbo answers a room admin with <c>room.error.variable_permissions</c>). The room
    /// clearing itself always may.
    /// </summary>
    public async Task<bool> CanPickUpVariableAsync(ActionContext ctx, IRoomItem item) =>
        ctx.Origin == ActionOrigin.System
        || item.OwnerId == ctx.PlayerId
        || await GetIsRoomOwnerAsync(ctx);

    public Task<bool> GetIsRoomOwnerAsync(ActionContext ctx) => GetIsRoomOwnerAsync(ctx.PlayerId);

    /// <summary>
    /// Whether the player may act as this room's owner: it is theirs, or they control every room.
    /// Asks the permission grain for a player who is not here.
    /// </summary>
    public async Task<bool> GetIsRoomOwnerAsync(PlayerId playerId) =>
        _roomGrain._state.RoomSnapshot.OwnerId == playerId
        || await HasPermissionAsync(playerId, PermissionNodes.Room.CONTROL_ANY);

    /// <summary>
    /// Whether the player may act as this room's owner. Synchronous, for callers that cannot await
    /// (the wired variables), so it reads the permissions on the player's avatar: a staff member
    /// not standing in the room counts only through <see cref="GetIsRoomOwnerAsync(PlayerId)"/>.
    /// </summary>
    public bool IsRoomOwner(PlayerId playerId) =>
        _roomGrain._state.RoomSnapshot.OwnerId == playerId
        || HasPermission(playerId, PermissionNodes.Room.CONTROL_ANY);

    /// <summary>
    /// Whether the room is this player's own, and nobody else's: for what ownership itself means
    /// (an owner does not rate their own room; the wired `@is_owner` flag), not for what an owner
    /// may do, which staff controlling every room may do too.
    /// </summary>
    public bool IsOwnedBy(PlayerId playerId) => _roomGrain._state.RoomSnapshot.OwnerId == playerId;

    /// <summary>
    /// Whether a player holds a permission node, from their avatar: synchronous, for a player in
    /// the room only. Anyone else holds nothing here.
    /// </summary>
    public bool HasPermission(PlayerId playerId, string node) =>
        AvatarModule.TryGetPlayer(playerId, out var player) && HasPermission(player, node);

    /// <summary>
    /// Whether a player in the room holds a node, from their avatar's copy of their permissions.
    /// Every room check of a player's node goes through here, so <c>perm verbose</c> sees it, and
    /// a node nobody registered is logged as the permission grain logs it: a bug in the gate,
    /// most likely a plugin's typo, not a denial.
    /// </summary>
    public bool HasPermission(IRoomPlayer player, string node)
    {
        var held = player.Permissions.Has(node);

        // Only a denial can be for an unregistered node, so a grant skips the registry.
        if (!held && !_roomGrain._permissionRegistryProvider.Current.IsCheckable(node))
            _roomGrain._logger.LogWarning(
                "Permission check for unregistered node {Node} on player {PlayerId} in room {RoomId}; denied",
                node,
                player.PlayerId,
                _roomGrain._state.RoomId
            );

        if (player.Permissions.IsWatched(node))
            _roomGrain._logger.LogInformation(
                "Verbose: player {PlayerId} checked {Node} in room {RoomId}: {Held}",
                player.PlayerId,
                node,
                _roomGrain._state.RoomId,
                held
            );

        return held;
    }

    /// <summary>
    /// Whether a player holds a permission node: from their avatar when they are in the room,
    /// which every furni move asks, otherwise from their permission grain.
    /// </summary>
    public async Task<bool> HasPermissionAsync(PlayerId playerId, string node)
    {
        if (AvatarModule.TryGetPlayer(playerId, out var player))
            return HasPermission(player, node);

        return playerId > 0
            && await _roomGrain._grainFactory.HasPermissionAsync(
                playerId,
                node,
                CancellationToken.None
            );
    }

    /// <summary>
    /// Whether this player was given rights here. Rights are loaded with the room, so this
    /// needs no await; it says nothing about the owner, who needs no rights.
    /// </summary>
    public bool HasRights(PlayerId playerId) =>
        _roomGrain._state.PlayerIdsWithRights.Contains(playerId);

    /// <summary>
    /// System-originated actions act as a moderator; everything else resolves by player id.
    /// </summary>
    public Task<RoomControllerType> GetControllerLevelAsync(ActionContext ctx) =>
        ctx.Origin == ActionOrigin.System
            ? Task.FromResult(RoomControllerType.Moderator)
            : GetControllerLevelAsync(ctx.PlayerId);

    public async Task<RoomControllerType> GetControllerLevelAsync(PlayerId playerId)
    {
        // Control of every room is the one hotel-wide permission a room level carries, and the
        // client knows it only as Moderator (and as security level 5, which the same node
        // projects to): see docs/permissions-client-gates.md. It is asked before ownership,
        // because a staff member is no less in their own room: the boxes only they may save
        // (Give Reward, Achievement Enabler) would otherwise refuse them there.
        if (await HasPermissionAsync(playerId, PermissionNodes.Room.CONTROL_ANY))
            return RoomControllerType.Moderator;

        if (IsOwnedBy(playerId))
            return RoomControllerType.Owner;

        var guild = await _roomGrain.GetGuildAsync(CancellationToken.None);

        if (guild is not null)
            return await GetGroupLevelAsync(guild, playerId);

        if (HasRights(playerId))
            return RoomControllerType.Rights;

        return RoomControllerType.None;
    }

    /// <summary>
    /// A player's level in a group homeroom. Rights there are the group's, not the room's:
    /// room_rights rows are ignored, and giving or taking them is refused for a group room
    /// elsewhere in this file. Admins and the owner manage; a plain member gets whatever the
    /// group's decoration setting gives, which the room already holds on the group summary.
    /// <para>
    /// The rank is asked of the group once per player and kept, because every build, pick-up
    /// and wired check asks it. The group tells the room whenever the answer can change
    /// (<see cref="ForgetGroupLevel"/> from a member's rank, <see cref="ForgetGroupLevels"/>
    /// from the group's own settings, which also re-reads the summary), and the room forgets a
    /// player when they leave.
    /// </para>
    /// </summary>
    private async Task<RoomControllerType> GetGroupLevelAsync(
        GuildSummarySnapshot guild,
        PlayerId playerId
    )
    {
        var levels = _roomGrain._state.GroupLevelByPlayerId;

        if (levels.TryGetValue(playerId, out var known))
            return known;

        var rank = await _roomGrain
            ._grainFactory.GetGuildGrain(guild.GuildId)
            .GetMemberRankAsync(playerId, CancellationToken.None);

        var level = rank switch
        {
            GuildMemberRank.Owner or GuildMemberRank.Admin => RoomControllerType.GroupAdmin,
            GuildMemberRank.Member when guild.RightsLevel == GuildRightsLevel.Members =>
                RoomControllerType.GroupRights,
            _ => RoomControllerType.None,
        };

        levels[playerId] = level;

        return level;
    }

    /// <summary>One player's standing in the group changed, or they left the room.</summary>
    internal void ForgetGroupLevel(PlayerId playerId) =>
        _roomGrain._state.GroupLevelByPlayerId.Remove(playerId);

    /// <summary>The group changed, or the room stopped or started being its homeroom.</summary>
    internal void ForgetGroupLevels() => _roomGrain._state.GroupLevelByPlayerId.Clear();

    public async Task RefreshControllerLevelForPlayerAsync(PlayerId playerId, CancellationToken ct)
    {
        var controllerLevel = await GetControllerLevelAsync(playerId);
        var inRoom = AvatarModule.TryGetPlayer(playerId, out var avatar);

        // Habbo tells YouAreController / YouAreNotController only when the level moved: a member
        // joining or kicked from the homeroom's group without rights is told neither (official
        // client captures: 2 joins, 2 kicks), one made or unmade an admin is (3 of 3).
        if (!inRoom || ShownControllerLevel(avatar!) != controllerLevel)
            await _roomGrain
                ._grainFactory.GetPlayerPresenceGrain(playerId)
                .OnControllerLevelUpdatedAsync(_roomGrain.RoomId, controllerLevel, ct);

        // Whatever else depends on the level (the wired permissions a player is shown) hears it
        // here, so this module does not have to know who that is.
        await _roomGrain.PublishRoomEventAsync(
            new PlayerControllerLevelChangedEvent
            {
                RoomId = _roomGrain.RoomId,
                CausedBy = ActionContext.CreateForSystem(_roomGrain.RoomId),
                PlayerId = playerId,
                ControllerLevel = controllerLevel,
            },
            ct
        );

        // Then, as in the entry, whether they may manage raid protection (official client
        // captures: after WiredPermissions on every level refresh, 7 of 7).
        if (inRoom)
            await _roomGrain._grainFactory.SendComposerToPlayerAsync(
                playerId,
                new RaidProtectionCapabilityMessageComposer
                {
                    RoomId = _roomGrain.RoomId,
                    CanManage = _roomGrain.CanManageRaidProtection(controllerLevel),
                },
                ct
            );

        ShowControllerLevel(playerId, controllerLevel);
    }

    /// <summary>The level the avatar shows (its <c>flatctrl</c> status), none if it shows none.</summary>
    private static RoomControllerType ShownControllerLevel(IRoomPlayer avatar) =>
        avatar.Statuses.TryGetValue(AvatarStatusType.FlatControl, out var shown)
        && int.TryParse(shown, out var level)
            ? (RoomControllerType)level
            : RoomControllerType.None;

    /// <summary>
    /// The level of a player who has just walked in, shown on their avatar. What the client is
    /// told of it goes in their entry (<c>RoomGrain.BuildEntryTailAsync</c>), not from here.
    /// </summary>
    public async Task<RoomControllerType> TakeEntryControllerLevelAsync(PlayerId playerId)
    {
        var controllerLevel = await GetControllerLevelAsync(playerId);

        ShowControllerLevel(playerId, controllerLevel);

        return controllerLevel;
    }

    private void ShowControllerLevel(PlayerId playerId, RoomControllerType controllerLevel)
    {
        if (!AvatarModule.TryGetPlayer(playerId, out var avatar))
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

        await RevokeRightsAsync(playerId, _roomGrain._state.RoomSnapshot.OwnerId, ct);

        return true;
    }

    /// <summary>
    /// Takes one player's rights away and tells <paramref name="toldPlayerId"/>, whose open
    /// rights list drops the entry: the owner when a player gives their rights up, the actor
    /// when the owner takes them. Giving up and taking away were two copies of this.
    /// </summary>
    private async Task RevokeRightsAsync(
        PlayerId playerId,
        PlayerId toldPlayerId,
        CancellationToken ct
    )
    {
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
            toldPlayerId,
            RightsRemovedComposer(playerId),
            ct
        );
    }

    private FlatControllerRemovedEventMessageComposer RightsRemovedComposer(PlayerId playerId) =>
        new() { RoomId = _roomGrain.RoomId, PlayerId = playerId };

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

        await RevokeRightsAsync(playerId, ctx.PlayerId, ct);
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

        // One entry per player for the actor's open rights list, as one batch to one player: the
        // list overload is the sanctioned direct presence call (see AGENTS.md), and it keeps order.
        await _roomGrain
            ._grainFactory.GetPlayerPresenceGrain(ctx.PlayerId)
            .SendComposerAsync([.. playerIds.Select(RightsRemovedComposer)], ct);
    }

    internal async Task EnsureRightsLoadedAsync(CancellationToken ct)
    {
        if (_roomGrain._state.IsRightsLoaded)
            return;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var entities = await dbCtx
            .RoomRights.AsNoTracking()
            .Where(x => x.RoomEntityId == (int)_roomGrain.RoomId)
            .ToListAsync(ct);

        foreach (var entity in entities)
            _roomGrain._state.PlayerIdsWithRights.Add(PlayerId.Parse(entity.PlayerEntityId));

        _roomGrain._state.IsRightsLoaded = true;
    }
}
