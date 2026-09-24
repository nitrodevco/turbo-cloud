using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Turbo.Primitives.Action;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Object;
using Turbo.Primitives.Rooms.Object.Avatars;
using Turbo.Rooms.Object.Logic.Furniture.Floor;

namespace Turbo.Rooms.Grains;

/// <summary>
/// The group side of a room: whether this room is a group's homeroom, and the group badges the
/// people standing in it wear.
///
/// The group is resolved through the guild directory, never through a group grain. The group
/// grain is on the other end of every controller-level check made here, and grains are not
/// reentrant, so the room asking a group grain anything would deadlock the pair the moment the
/// two happened to be talking about each other. The directory is a read-through cache and calls
/// nothing back.
/// </summary>
public sealed partial class RoomGrain
{
    /// <summary>
    /// Null means not looked up yet rather than "no group", which is why the flag below exists:
    /// a room that is nobody's homeroom must not re-ask on every check.
    /// </summary>
    private GuildSummarySnapshot? _guild;

    private bool _guildResolved;

    public async Task<GuildSummarySnapshot?> GetGuildAsync(CancellationToken ct)
    {
        if (_guildResolved)
            return _guild;

        try
        {
            _guild = await _grainFactory.GetGuildDirectoryGrain().GetGuildOfRoomAsync(RoomId, ct);
            _guildResolved = true;

            // The snapshot is built from the room row, which does not hold the group — the group
            // holds the room — so this is where the two meet. Every later change to the snapshot
            // is a `with`, which carries this along.
            if (_state.RoomSnapshot is not null)
                _state.RoomSnapshot = _state.RoomSnapshot with { Guild = _guild };
        }
        catch (Exception ex)
        {
            // A room that cannot reach the directory is treated as an ordinary room for now and
            // asks again next time. Treating it as a group room instead would hand out rights
            // nobody has been shown to hold.
            _logger.LogWarning(
                ex,
                "Could not resolve the group of room {RoomId}; treating it as an ordinary room",
                RoomId
            );
        }

        return _guild;
    }

    public async Task OnGuildChangedAsync(CancellationToken ct)
    {
        _guildResolved = false;
        _guild = null;

        await GetGuildAsync(ct);

        // Rights here are the group's, so whoever is standing in the room may now build when
        // they could not, or the other way round.
        foreach (var playerId in _state.AvatarsByPlayerId.Keys.ToList())
            await SecurityModule.RefreshControllerLevelForPlayerAsync(playerId, ct);
    }

    public async Task SetPlayerFavouriteGuildAsync(
        PlayerId playerId,
        int guildId,
        int guildStatus,
        string guildName,
        CancellationToken ct
    )
    {
        if (!AvatarModule.TryGetPlayer(playerId, out var player))
            return;

        player.SetFavouriteGuild(guildId, guildStatus, guildName);

        await SendComposerToRoomAsync(
            new FavoriteMembershipUpdateMessageComposer
            {
                RoomIndex = player.ObjectId,
                GuildId = guildId,
                Status = guildStatus,
                GuildName = guildName,
            },
            ct
        );
    }

    public async Task<bool> GetIsGroupRoomAsync(CancellationToken ct) =>
        await GetGuildAsync(ct) is not null;

    public async Task RefreshGuildFurniAsync(GuildId guildId, CancellationToken ct)
    {
        if (guildId <= 0)
            return;

        var furni = _state
            .ItemsById.Values.Select(item => item.Logic)
            .OfType<FurnitureGuildCustomizedLogic>()
            .Where(logic => logic.GuildId == guildId)
            .ToList();

        if (furni.Count == 0)
            return;

        // Once for the room, not once per item: every piece here wears the same group.
        var guild = await _grainFactory.GetGuildDirectoryGrain().GetSummaryAsync(guildId, ct);

        foreach (var logic in furni)
            await logic.ApplyGuildAsync(guild, ct);
    }

    public async Task SendGuildFurniContextMenuAsync(
        PlayerId viewerId,
        RoomObjectId objectId,
        CancellationToken ct
    )
    {
        if (
            !_state.ItemsById.TryGetValue(objectId, out var item)
            || item.Logic is not FurnitureGuildCustomizedLogic guildFurni
            || guildFurni.GuildId <= 0
        )
            return;

        var guild = await _grainFactory
            .GetGuildDirectoryGrain()
            .GetSummaryAsync(guildFurni.GuildId, ct);

        if (guild is null)
            return;

        // Asking the group grain is the allowed direction: the group never calls a room.
        var rank = await _grainFactory
            .GetGuildGrain(guild.GuildId)
            .GetMemberRankAsync(viewerId, ct);

        var isMember =
            rank is GuildMemberRank.Owner or GuildMemberRank.Admin or GuildMemberRank.Member;

        await _grainFactory.SendComposerToPlayerAsync(
            viewerId,
            new GuildFurniContextMenuInfoMessageComposer
            {
                ObjectId = objectId,
                GuildId = guild.GuildId,
                GuildName = guild.Name,
                GuildHomeRoomId = guild.RoomId,
                UserIsMember = isMember,
                // No group has a forum until the forum ship lands, and a group's forum is
                // readable by its own rules once it does.
                GuildHasReadableForum = guild.HasForum && isMember,
            },
            ct
        );
    }

    /// <summary>
    /// The group that called this room home is gone: every item standing here goes back to
    /// whoever owns it, and the room becomes an ordinary room again.
    ///
    /// A system context picks up as a moderator and sends each item to its owner rather than to
    /// the actor, which is exactly what the client's delete confirmation promises. One failure
    /// does not stop the rest: an item that will not go back is worth a log, not a homeroom full
    /// of furni nobody can take.
    /// </summary>
    public async Task OnGuildDeletedAsync(CancellationToken ct)
    {
        var ctx = ActionContext.CreateForSystem(RoomId);

        foreach (var itemId in _state.ItemsById.Keys.ToList())
        {
            try
            {
                await ActionModule.RemoveItemByIdAsync(ctx, itemId, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Could not return item {ItemId} to its owner while deleting the group of room {RoomId}",
                    itemId,
                    RoomId
                );
            }
        }

        await OnGuildChangedAsync(ct);
    }
}
