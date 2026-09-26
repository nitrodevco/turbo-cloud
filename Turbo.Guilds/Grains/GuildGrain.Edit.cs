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
using Turbo.Primitives.Texts;

namespace Turbo.Guilds.Grains;

/// <summary>
/// Changing a group. Every one of these is owner-only today: the client only opens the manage
/// window for an owner, and an admin's powers are over members rather than over the group
/// itself.
///
/// Each one saves, refreshes what this grain holds, tells the directory, nudges the actor's open
/// window with <c>GroupDetailsChanged</c>, and publishes whatever else the change means through
/// <c>GuildGrain.Notify</c>. None of that is the caller's to remember: a handler calls one method
/// and is done.
///
/// The nudge itself goes to the actor alone, because reaching everyone who might have the group
/// open would mean asking the room who is in it.
/// </summary>
internal sealed partial class GuildGrain
{
    public Task<int> GetMemberCountAsync(CancellationToken ct) => Task.FromResult(CountOfMembers());

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

        var clampedName = ClientText.Truncate(name, _guildConfig.NameMaxLength);

        if (string.IsNullOrWhiteSpace(clampedName))
            return false;

        await SaveAsync(
            entity =>
            {
                entity.Name = clampedName;
                entity.Description = ClientText.Truncate(
                    description,
                    _guildConfig.DescriptionMaxLength
                );
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

        await PublishChangedAsync(actorId, ct, repaintFurni: true);

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

        await PublishChangedAsync(actorId, ct, repaintFurni: true);

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

        if (CountOfMembers() > _guildConfig.DeletionMaxMembers)
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
        // confirmation promised. Told rather than awaited, like everything else this grain sends
        // a room: the ordering happens to make an awaited call safe here — the group is already
        // out of the directory, so the room resolves no group and cannot ask back — but that is
        // a property of the room's furni-removal path, not of this call, and it would be somebody
        // else's to preserve. Not awaiting it makes the pair safe whatever that path does later.
        _grainFactory
            .GetRoomGrain(guild.RoomId)
            .OnGuildDeletedAsync(CancellationToken.None)
            .LogAndForget(
                _logger,
                $"return the homeroom furni of deleted group {guild.GuildId.Value}"
            );

        // Each member's own grain caches its memberships, so each is told; the presences are
        // separate grains, so the sends run side by side.
        await Task.WhenAll(
            memberIds.Select(playerId =>
                _grainFactory.GetPlayerGuildGrain(playerId).OnMembershipsChangedAsync(ct)
            )
        );

        // Furni of this group standing in other rooms did not come back with the homeroom's, and
        // is now wearing a badge that resolves to nothing.
        NotifyGuildFurniChanged();

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

        // The group row only: an edit never moves the roster.
        await LoadGuildAsync(ct);
    }

    /// <summary>
    /// Everything a change to the group means for everyone else: the directory learns what the
    /// group is now, the homeroom re-reads it, the actor's open window is nudged to ask again,
    /// and — when the change was one that can be seen — the group's furni is repainted wherever
    /// it stands.
    /// </summary>
    /// <param name="repaintFurni">
    /// Whether the badge or the colours moved. A rename changes nothing about how the furni
    /// looks, and repainting on one would walk every loaded room for nothing.
    /// </param>
    private async Task PublishChangedAsync(
        PlayerId actorId,
        CancellationToken ct,
        bool repaintFurni = false
    )
    {
        if (_state.Guild is not { } guild)
            return;

        await _grainFactory.GetGuildDirectoryGrain().OnGuildChangedAsync(guild, ct);

        // Told, not asked: see GuildGrain.Notify for why none of this is awaited.
        NotifyHomeroomGuildChanged();

        if (repaintFurni)
            NotifyGuildFurniChanged();

        await _grainFactory.SendComposerToPlayerAsync(
            actorId,
            new GroupDetailsChangedMessageComposer { GuildId = guild.GuildId },
            ct
        );
    }
}
