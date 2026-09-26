using System;
using System.Collections.Generic;
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
/// The roster: joining, approving, promoting, kicking and blocking.
///
/// This is what the group grain's single-threading is for. Two admins approving the same request
/// arrive here one after the other, and the second finds the rank already changed and is told
/// so, rather than both writing and one silently winning.
///
/// Each change writes the row, updates the roster held here, tells the member's own grain that
/// its cached memberships moved, refreshes their rights in the homeroom, and sends whatever the
/// client needs to redraw. All of it happens in <see cref="AddOrUpdateMemberAsync"/> and
/// <see cref="RemoveMemberAsync"/>, which every one of the operations above goes through, so
/// none of the callers has to remember any of it.
/// </summary>
internal sealed partial class GuildGrain
{
    public async Task<GuildJoinResultSnapshot> JoinAsync(PlayerId playerId, CancellationToken ct)
    {
        if (_state.Guild is not { } guild)
            return GuildJoinResultSnapshot.Failed(GuildJoinFailedType.GroupClosed);

        var rank = GetRank(playerId);

        if (rank == GuildMemberRank.Blocked)
            return GuildJoinResultSnapshot.Failed(GuildJoinFailedType.GroupClosed);

        // Already in, or already waiting: nothing to do and nothing to complain about.
        if (rank is not null)
            return rank == GuildMemberRank.Requested
                ? GuildJoinResultSnapshot.Requested()
                : GuildJoinResultSnapshot.Joined();

        var wanted = guild.Type switch
        {
            GuildType.Regular or GuildType.Open or GuildType.Large => GuildMemberRank.Member,
            GuildType.Exclusive => GuildMemberRank.Requested,
            _ => (GuildMemberRank?)null,
        };

        if (wanted is not { } newRank)
            return GuildJoinResultSnapshot.Failed(GuildJoinFailedType.GroupClosed);

        if (newRank == GuildMemberRank.Member && FreeMemberSlots() == 0)
            return GuildJoinResultSnapshot.Failed(GuildJoinFailedType.GroupFull);

        // The joining player's own limit. Their grain owns that count; it never calls back here,
        // so asking it is safe.
        if (!await CanTakeAnotherGroupAsync(playerId, ct))
            return GuildJoinResultSnapshot.Failed(GuildJoinFailedType.TooManyGroups);

        await AddOrUpdateMemberAsync(playerId, newRank, ct);

        if (newRank == GuildMemberRank.Requested)
        {
            var requester = await BuildMemberSnapshotAsync(playerId, ct);

            if (requester is not null)
            {
                await _grainFactory.SendComposerToPlayersAsync(
                    ManagerIds(),
                    new GroupMembershipRequestedMessageComposer
                    {
                        GuildId = guild.GuildId,
                        Member = requester,
                    },
                    ct
                );
            }

            return GuildJoinResultSnapshot.Requested();
        }

        await PublishMembershipUpdatedAsync(playerId, ct);

        return GuildJoinResultSnapshot.Joined();
    }

    public async Task<GuildMemberMgmtResultSnapshot> ApproveRequestAsync(
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        if (!CanManage(actorId))
            return GuildMemberMgmtResultSnapshot.Refused();

        var rank = GetRank(targetId);

        if (rank is null)
            return GuildMemberMgmtResultSnapshot.Failed(GuildMemberMgmtFailedType.NoLongerMember);

        if (rank != GuildMemberRank.Requested)
            return GuildMemberMgmtResultSnapshot.Failed(GuildMemberMgmtFailedType.AlreadyAccepted);

        if (FreeMemberSlots() == 0)
            return GuildMemberMgmtResultSnapshot.FailedToJoin(GuildJoinFailedType.GroupFull);

        // Their limit, not this group's, and the hotel words it as being about them: whether
        // they are short of a club membership or have simply run out of room.
        if (!await CanTakeAnotherGroupAsync(targetId, ct))
            return GuildMemberMgmtResultSnapshot.FailedToJoin(
                await _grainFactory.HasActiveClubAsync(targetId, ct)
                    ? GuildJoinFailedType.TargetAtMaxMemberships
                    : GuildJoinFailedType.TargetNotClubMember
            );

        await AddOrUpdateMemberAsync(targetId, GuildMemberRank.Member, ct);
        await PublishMembershipUpdatedAsync(targetId, ct);

        return GuildMemberMgmtResultSnapshot.Success();
    }

    public async Task<GuildMemberMgmtResultSnapshot> RejectRequestAsync(
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        if (!CanManage(actorId))
            return GuildMemberMgmtResultSnapshot.Refused();

        var rank = GetRank(targetId);

        if (rank is null)
            return GuildMemberMgmtResultSnapshot.Failed(GuildMemberMgmtFailedType.NoLongerMember);

        if (rank != GuildMemberRank.Requested)
            return GuildMemberMgmtResultSnapshot.Failed(GuildMemberMgmtFailedType.AlreadyRejected);

        await RemoveMemberAsync(targetId, ct);

        if (_state.Guild is { } guild)
        {
            await _grainFactory.SendComposerToPlayerAsync(
                actorId,
                new GuildMembershipRejectedMessageComposer
                {
                    GuildId = guild.GuildId,
                    PlayerId = targetId,
                },
                ct
            );
        }

        return GuildMemberMgmtResultSnapshot.Success();
    }

    public async Task<GuildMemberMgmtResultSnapshot> ApproveAllRequestsAsync(
        PlayerId actorId,
        CancellationToken ct
    )
    {
        if (!CanManage(actorId))
            return GuildMemberMgmtResultSnapshot.Refused();

        var pending = _state
            .RankByPlayerId.Where(entry => entry.Value == GuildMemberRank.Requested)
            .Select(entry => PlayerId.Parse(entry.Key))
            .ToList();

        if (pending.Count == 0)
            return GuildMemberMgmtResultSnapshot.Success();

        // Each player's own limit is theirs, so each is asked — but side by side, not one after
        // the other, because the answers do not depend on each other.
        var allowed = await Task.WhenAll(
            pending.Select(async playerId =>
                (playerId, ok: await CanTakeAnotherGroupAsync(playerId, ct))
            )
        );

        var eligible = allowed.Where(x => x.ok).Select(x => x.playerId).ToList();

        // The group's cap applies to a batch exactly as to one approval; whoever does not fit
        // stays requested, and the manager is told the group is full.
        var joining = eligible.Take(FreeMemberSlots()).ToList();
        var result =
            joining.Count < eligible.Count
                ? GuildMemberMgmtResultSnapshot.FailedToJoin(GuildJoinFailedType.GroupFull)
                : GuildMemberMgmtResultSnapshot.Success();

        if (joining.Count == 0)
            return result;

        // One transaction for the lot rather than one per member: approving a busy group's
        // backlog is exactly the case this is for.
        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var ids = joining.Select(x => x.Value).ToList();

            await dbCtx
                .GuildMembers.Where(x =>
                    x.GuildEntityId == GuildId.Value && ids.Contains(x.PlayerEntityId)
                )
                .ExecuteUpdateAsync(x => x.SetProperty(m => m.Rank, GuildMemberRank.Member), ct);
        }

        foreach (var playerId in joining)
            _state.RankByPlayerId[playerId.Value] = GuildMemberRank.Member;

        await Task.WhenAll(
            joining.Select(playerId =>
                _grainFactory.GetPlayerGuildGrain(playerId).OnMembershipsChangedAsync(ct)
            )
        );

        // The batch wrote its own rows rather than going through AddOrUpdateMemberAsync, so it
        // publishes for itself too.
        foreach (var playerId in joining)
            NotifyHomeroomMemberChanged(playerId);

        await Task.WhenAll(joining.Select(playerId => PublishMembershipUpdatedAsync(playerId, ct)));

        return result;
    }

    public async Task<GuildMemberMgmtResultSnapshot> SetAdminAsync(
        PlayerId actorId,
        PlayerId targetId,
        bool isAdmin,
        CancellationToken ct
    )
    {
        // Admins are the owner's to appoint. An admin promoting another admin would let anyone
        // who was ever trusted hand the group around.
        if (_state.Guild is not { } guild || guild.OwnerId != actorId)
            return GuildMemberMgmtResultSnapshot.Refused();

        if (targetId == guild.OwnerId)
            return GuildMemberMgmtResultSnapshot.Refused();

        var rank = GetRank(targetId);

        if (rank is not (GuildMemberRank.Member or GuildMemberRank.Admin))
            return GuildMemberMgmtResultSnapshot.Failed(GuildMemberMgmtFailedType.NoLongerMember);

        await AddOrUpdateMemberAsync(
            targetId,
            isAdmin ? GuildMemberRank.Admin : GuildMemberRank.Member,
            ct
        );

        await PublishMembershipUpdatedAsync(targetId, ct);

        return GuildMemberMgmtResultSnapshot.Success();
    }

    /// <summary>
    /// Removing somebody, which is also how a member leaves: the client sends the same packet
    /// with itself as the target.
    /// </summary>
    public async Task<GuildMemberMgmtResultSnapshot> KickAsync(
        PlayerId actorId,
        PlayerId targetId,
        bool block,
        CancellationToken ct
    )
    {
        if (_state.Guild is not { } guild)
            return GuildMemberMgmtResultSnapshot.Refused();

        // The owner cannot be removed by anyone, themselves included; they delete the group.
        if (targetId == guild.OwnerId)
            return GuildMemberMgmtResultSnapshot.Refused();

        var leaving = actorId == targetId;

        if (!leaving && !CanManage(actorId))
            return GuildMemberMgmtResultSnapshot.Refused();

        // An admin is the owner's to remove, the same as appointing one.
        if (!leaving && GetRank(targetId) == GuildMemberRank.Admin && guild.OwnerId != actorId)
            return GuildMemberMgmtResultSnapshot.Refused();

        if (GetRank(targetId) is null)
            return GuildMemberMgmtResultSnapshot.Failed(GuildMemberMgmtFailedType.NoLongerMember);

        if (block && !leaving && _guildConfig.BlockingEnabled)
            await AddOrUpdateMemberAsync(targetId, GuildMemberRank.Blocked, ct);
        else
            await RemoveMemberAsync(targetId, ct);

        await PublishMembershipUpdatedAsync(targetId, ct);

        return GuildMemberMgmtResultSnapshot.Success();
    }

    public async Task<GuildMemberMgmtResultSnapshot> UnblockAsync(
        PlayerId actorId,
        PlayerId targetId,
        CancellationToken ct
    )
    {
        if (!CanManage(actorId))
            return GuildMemberMgmtResultSnapshot.Refused();

        if (GetRank(targetId) != GuildMemberRank.Blocked)
            return GuildMemberMgmtResultSnapshot.Failed(GuildMemberMgmtFailedType.NoLongerMember);

        await RemoveMemberAsync(targetId, ct);

        return GuildMemberMgmtResultSnapshot.Success();
    }

    /// <summary>
    /// A page of the roster. This is the one read that queries rather than answering from the
    /// roster held here: filtering by name needs the players' names, which belong to the players.
    /// </summary>
    public async Task<GuildMemberPageSnapshot?> GetMembersPageAsync(
        PlayerId viewerId,
        int pageIndex,
        string nameFilter,
        GuildMemberSearchType searchType,
        CancellationToken ct
    )
    {
        if (_state.Guild is not { } guild)
            return null;

        var allowedToManage = CanManage(viewerId);

        // Pending and blocked are management views; a client that asks for one without the right
        // to is answered with the whole roster rather than refused.
        var effectiveSearch =
            allowedToManage
            || searchType is GuildMemberSearchType.All or GuildMemberSearchType.Admins
                ? searchType
                : GuildMemberSearchType.All;

        var ranks = effectiveSearch switch
        {
            GuildMemberSearchType.Admins => GuildMemberRanks.ManagingRanks(),
            GuildMemberSearchType.Pending => [GuildMemberRank.Requested],
            GuildMemberSearchType.Blocked => [GuildMemberRank.Blocked],
            _ => GuildMemberRanks.MemberRanks(),
        };

        var pageSize = _guildConfig.MembersPageSize;
        var page = Math.Max(0, pageIndex);
        var filter = nameFilter ?? string.Empty;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var query =
            from member in dbCtx.GuildMembers.AsNoTracking()
            join player in dbCtx.Players.AsNoTracking() on member.PlayerEntityId equals player.Id
            where member.GuildEntityId == GuildId.Value && ranks.Contains(member.Rank)
            select new
            {
                member.PlayerEntityId,
                member.Rank,
                member.CreatedAt,
                player.Name,
                player.Figure,
            };

        if (!string.IsNullOrEmpty(filter))
            query = query.Where(x => x.Name.Contains(filter));

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderBy(x => x.Rank)
            .ThenBy(x => x.PlayerEntityId)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new GuildMemberPageSnapshot
        {
            Guild = guild,
            TotalEntries = total,
            Members =
            [
                .. rows.Select(row => new GuildMemberSnapshot
                {
                    Rank = row.Rank,
                    PlayerId = PlayerId.Parse(row.PlayerEntityId),
                    PlayerName = row.Name ?? string.Empty,
                    Figure = row.Figure ?? string.Empty,
                    MemberSince = row.CreatedAt,
                }),
            ],
            AllowedToManage = allowedToManage,
            PageSize = pageSize,
            PageIndex = page,
            SearchType = effectiveSearch,
            NameFilter = filter,
        };
    }

    /// <summary>The owner and the group's admins act on members; nobody else does.</summary>
    private bool CanManage(PlayerId actorId) =>
        _state.Guild is { } guild
        && (guild.OwnerId == actorId || GuildMemberRanks.CanManage(GetRank(actorId)));

    /// <summary>
    /// Whether this player has room for another group. The cap is theirs, not the group's, so
    /// their own grain answers it; that grain never calls back here.
    /// </summary>
    private async Task<bool> CanTakeAnotherGroupAsync(PlayerId playerId, CancellationToken ct)
    {
        var membershipsTask = _grainFactory
            .GetPlayerGuildGrain(playerId)
            .GetMembershipCountAsync(ct);
        var clubTask = _grainFactory.HasActiveClubAsync(playerId, ct);

        await Task.WhenAll(membershipsTask, clubTask);

        return await membershipsTask
            < (await clubTask ? _guildConfig.MembershipsMaxWithClub : _guildConfig.MembershipsMax);
    }

    private async Task AddOrUpdateMemberAsync(
        PlayerId playerId,
        GuildMemberRank rank,
        CancellationToken ct
    )
    {
        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            var entity = await dbCtx.GuildMembers.FirstOrDefaultAsync(
                x => x.GuildEntityId == GuildId.Value && x.PlayerEntityId == playerId.Value,
                ct
            );

            if (entity is null)
            {
                dbCtx.GuildMembers.Add(
                    new GuildMemberEntity
                    {
                        GuildEntityId = GuildId.Value,
                        PlayerEntityId = playerId.Value,
                        Rank = rank,
                        IsFavourite = false,
                    }
                );
            }
            else
            {
                entity.Rank = rank;
            }

            await dbCtx.SaveChangesAsync(ct);
        }

        _state.RankByPlayerId[playerId.Value] = rank;

        await _grainFactory.GetPlayerGuildGrain(playerId).OnMembershipsChangedAsync(ct);

        NotifyHomeroomMemberChanged(playerId);
    }

    private async Task RemoveMemberAsync(PlayerId playerId, CancellationToken ct)
    {
        await using (var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct))
        {
            await dbCtx
                .GuildMembers.Where(x =>
                    x.GuildEntityId == GuildId.Value && x.PlayerEntityId == playerId.Value
                )
                .ExecuteDeleteAsync(ct);
        }

        _state.RankByPlayerId.Remove(playerId.Value);

        await _grainFactory.GetPlayerGuildGrain(playerId).OnMembershipsChangedAsync(ct);

        NotifyHomeroomMemberChanged(playerId);
    }

    /// <summary>The owner and the admins: who hears about a roster change.</summary>
    private List<PlayerId> ManagerIds() =>
        [
            .. _state
                .RankByPlayerId.Where(entry => GuildMemberRanks.CanManage(entry.Value))
                .Select(entry => PlayerId.Parse(entry.Key)),
        ];

    /// <summary>Tells the group's managers, and the member themselves, that a rank moved.</summary>
    private async Task PublishMembershipUpdatedAsync(PlayerId playerId, CancellationToken ct)
    {
        if (_state.Guild is not { } guild)
            return;

        var member = await BuildMemberSnapshotAsync(playerId, ct);

        if (member is null)
            return;

        var recipients = ManagerIds().Append(playerId).Distinct().ToList();

        await _grainFactory.SendComposerToPlayersAsync(
            recipients,
            new GuildMembershipUpdatedMessageComposer { GuildId = guild.GuildId, Member = member },
            ct
        );
    }

    /// <summary>
    /// The member as a packet carries them. Their name and figure are the player's, so they are
    /// read here rather than kept in the roster.
    /// </summary>
    private async Task<GuildMemberSnapshot?> BuildMemberSnapshotAsync(
        PlayerId playerId,
        CancellationToken ct
    )
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var row = await dbCtx
            .Players.AsNoTracking()
            .Where(x => x.Id == playerId.Value)
            .Select(x => new { x.Name, x.Figure })
            .FirstOrDefaultAsync(ct);

        if (row is null)
            return null;

        // When they are gone the row is gone with them, and the client only needs a date to
        // print; falling back to now beats leaving the field unset.
        var memberSince =
            await dbCtx
                .GuildMembers.AsNoTracking()
                .Where(x => x.GuildEntityId == GuildId.Value && x.PlayerEntityId == playerId.Value)
                .Select(x => (DateTime?)x.CreatedAt)
                .FirstOrDefaultAsync(ct)
            ?? DateTime.UtcNow;

        return new GuildMemberSnapshot
        {
            // Somebody just removed keeps the rank the client should draw them as gone from.
            Rank = GetRank(playerId) ?? GuildMemberRank.Requested,
            PlayerId = playerId,
            PlayerName = row.Name ?? string.Empty,
            Figure = row.Figure ?? string.Empty,
            MemberSince = memberSince,
        };
    }
}
