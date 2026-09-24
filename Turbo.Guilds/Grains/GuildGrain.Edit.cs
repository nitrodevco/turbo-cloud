using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Guilds;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Enums;
using Turbo.Primitives.Guilds.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Users;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Guilds.Grains;

/// <summary>
/// Changing a group. Every one of these is owner-only today: the client only opens the manage
/// window for an owner, and an admin's powers are over members rather than over the group
/// itself.
///
/// Each one saves, refreshes what this grain holds, tells the directory, and nudges the actor's
/// open window with <c>GroupDetailsChanged</c>. The nudge goes to the actor alone because
/// reaching everyone who might have the group open means the room, and this grain does not call
/// the room grain: the room answers a rights check by asking this grain, so a call out to it
/// while this one is still running would have the two waiting on each other.
///
/// Deletion is the one exception, and only because it happens after the group is gone from the
/// directory — by then the room resolves no group at all and cannot ask this grain anything.
/// </summary>
internal sealed partial class GuildGrain
{
    public Task<int> GetMemberCountAsync(CancellationToken ct) =>
        Task.FromResult(
            CountOfRanks(GuildMemberRank.Owner, GuildMemberRank.Admin, GuildMemberRank.Member)
        );

    public Task<ImmutableArray<GuildBadgePartSnapshot>> GetBadgePartsAsync(CancellationToken ct) =>
        Task.FromResult(GuildBadgeCodes.Parse(_state.Guild?.BadgeCode));

    public async Task<bool> UpdateIdentityAsync(
        PlayerId actorId,
        string name,
        string description,
        CancellationToken ct
    )
    {
        if (!CanEdit(actorId))
            return false;

        var clampedName = Clamp(name, _guildConfig.NameMaxLength);

        if (string.IsNullOrWhiteSpace(clampedName))
            return false;

        await SaveAsync(
            entity =>
            {
                entity.Name = clampedName;
                entity.Description = Clamp(description, _guildConfig.DescriptionMaxLength);
            },
            ct
        );

        await PublishChangedAsync(actorId, ct);

        return true;
    }

    public async Task<bool> UpdateBadgeAsync(
        PlayerId actorId,
        ImmutableArray<GuildBadgePartSnapshot> badgeParts,
        CancellationToken ct
    )
    {
        if (!CanEdit(actorId))
            return false;

        var editorData = await _grainFactory.GetGuildDirectoryGrain().GetEditorDataAsync(ct);
        var badgeCode = GuildBadgeCodes.Build(GuildBadgeParts.Sanitize(badgeParts, editorData));

        if (string.IsNullOrEmpty(badgeCode))
            return false;

        await SaveAsync(entity => entity.BadgeCode = badgeCode, ct);

        await PublishChangedAsync(actorId, ct);

        return true;
    }

    public async Task<bool> UpdateColorsAsync(
        PlayerId actorId,
        int primaryColorId,
        int secondaryColorId,
        CancellationToken ct
    )
    {
        if (!CanEdit(actorId))
            return false;

        var editorData = await _grainFactory.GetGuildDirectoryGrain().GetEditorDataAsync(ct);

        await SaveAsync(
            entity =>
            {
                entity.PrimaryColorId = GuildBadgeParts.PickColorId(
                    editorData.PrimaryColors,
                    primaryColorId
                );
                entity.SecondaryColorId = GuildBadgeParts.PickColorId(
                    editorData.SecondaryColors,
                    secondaryColorId
                );
            },
            ct
        );

        await PublishChangedAsync(actorId, ct);

        return true;
    }

    public async Task<bool> UpdateSettingsAsync(
        PlayerId actorId,
        GuildType guildType,
        GuildRightsLevel rightsLevel,
        CancellationToken ct
    )
    {
        if (!CanEdit(actorId))
            return false;

        // The editor offers three types; the other two belong to the hotel's own groups, and a
        // client that sends one of those is answered with the closest thing it may set.
        var type = guildType switch
        {
            GuildType.Regular or GuildType.Exclusive or GuildType.Private => guildType,
            _ => GuildType.Regular,
        };

        var rights = rightsLevel switch
        {
            GuildRightsLevel.Owner or GuildRightsLevel.Admins or GuildRightsLevel.Members =>
                rightsLevel,
            _ => GuildRightsLevel.Admins,
        };

        await SaveAsync(
            entity =>
            {
                entity.GuildType = type;
                entity.RightsLevel = rights;
            },
            ct
        );

        await PublishChangedAsync(actorId, ct);

        return true;
    }

    public async Task<bool> DeactivateAsync(PlayerId actorId, CancellationToken ct)
    {
        if (_state.Guild is not { } guild || guild.OwnerId != actorId)
            return false;

        if (!_guildConfig.DeletionEnabled)
            return false;

        var memberIds = _state.RankByPlayerId.Keys.Select(PlayerId.Parse).ToList();

        if (
            CountOfRanks(GuildMemberRank.Owner, GuildMemberRank.Admin, GuildMemberRank.Member)
            > _guildConfig.DeletionMaxMembers
        )
            return false;

        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            // Tracked removes rather than two ExecuteDelete calls: the members and the group go
            // in one transaction, so a group can never outlive its roster or the other way round.
            var entity = await dbCtx.Guilds.FirstOrDefaultAsync(x => x.Id == GuildId.Value, ct);

            if (entity is null)
                return false;

            var members = await dbCtx
                .GuildMembers.Where(x => x.GuildEntityId == GuildId.Value)
                .ToListAsync(ct);

            dbCtx.GuildMembers.RemoveRange(members);
            dbCtx.Guilds.Remove(entity);

            await dbCtx.SaveChangesAsync(ct);
        }

        _state.Guild = null;
        _state.RankByPlayerId.Clear();

        await _grainFactory.GetGuildDirectoryGrain().OnGuildRemovedAsync(guild.GuildId, ct);

        // The homeroom hands its furni back to whoever owns it, which is what the client's
        // confirmation promised. This is the one call this grain makes into a room, and it is
        // safe because the group no longer exists: the room can resolve nothing back to here.
        await _grainFactory.GetRoomGrain(guild.RoomId).OnGuildDeletedAsync(ct);

        // Each member's own grain caches its memberships, so each is told; the presences are
        // separate grains, so the sends run side by side.
        await Task.WhenAll(
            memberIds.Select(playerId =>
                _grainFactory.GetPlayerGuildGrain(playerId).OnMembershipsChangedAsync(ct)
            )
        );

        await _grainFactory.SendComposerToPlayersAsync(
            memberIds,
            new HabboGroupDeactivatedMessageComposer { GuildId = guild.GuildId },
            ct
        );

        DeactivateOnIdle();

        return true;
    }

    /// <summary>Only the owner edits the group itself; an admin's powers are over its members.</summary>
    private bool CanEdit(PlayerId actorId) => _state.Guild is { } guild && guild.OwnerId == actorId;

    private static string Clamp(string? value, int maxLength) =>
        string.IsNullOrEmpty(value) ? string.Empty
        : value.Length <= maxLength ? value
        : value[..maxLength];

    /// <summary>Writes the row, then rebuilds what this grain holds from it.</summary>
    private async Task SaveAsync(Action<GuildEntity> change, CancellationToken ct)
    {
        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var entity = await dbCtx.Guilds.FirstOrDefaultAsync(x => x.Id == GuildId.Value, ct);

            if (entity is null)
                return;

            change(entity);

            await dbCtx.SaveChangesAsync(ct);
        }

        await LoadAsync(ct);
    }

    /// <summary>
    /// Tells the directory what the group is now, and nudges the actor's open window to ask for
    /// the details again.
    /// </summary>
    private async Task PublishChangedAsync(PlayerId actorId, CancellationToken ct)
    {
        if (_state.Guild is not { } guild)
            return;

        await _grainFactory.GetGuildDirectoryGrain().OnGuildChangedAsync(guild, ct);

        // The homeroom is not told from here. Rights there are this group's, so the room answers
        // a rights check by asking this grain — and this grain would be sitting inside that call
        // waiting for the room. The handler tells the room once this call has returned.

        await _grainFactory.SendComposerToPlayerAsync(
            actorId,
            new GroupDetailsChangedMessageComposer { GuildId = guild.GuildId },
            ct
        );
    }
}
