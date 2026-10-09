using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Turbo.Database.Context;
using Turbo.Database.Entities.Guilds;
using Turbo.Guilds.Configuration;
using Turbo.Primitives.Guilds;
using Turbo.Primitives.Guilds.Forums.Enums;
using Turbo.Primitives.Guilds.Forums.Snapshots;
using Turbo.Primitives.Guilds.Grains;
using Turbo.Primitives.Messages.Outgoing.Groupforums;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Guilds.Grains;

/// <summary>
/// A player's side of the group forums. Stateless: every answer is a query (the lists, the
/// unread count, polled every groupforum.poll.period) and every read marker is written through,
/// so there is nothing to hydrate or flush.
/// </summary>
internal sealed class PlayerGuildForumGrain : Grain, IPlayerGuildForumGrain
{
    private readonly IDbContextFactory<TurboDbContext> _dbCtxFactory;
    private readonly GuildConfig _config;
    private readonly IGrainFactory _grainFactory;
    private readonly ILogger<IPlayerGuildForumGrain> _logger;

    private PlayerId PlayerId => this.GetPlayerId();

    public PlayerGuildForumGrain(
        IDbContextFactory<TurboDbContext> dbCtxFactory,
        IOptions<GuildConfig> config,
        IGrainFactory grainFactory,
        ILogger<IPlayerGuildForumGrain> logger
    )
    {
        _dbCtxFactory = dbCtxFactory;
        _config = config.Value;
        _grainFactory = grainFactory;
        _logger = logger;
    }

    public async Task SendUnreadForumsCountAsync(CancellationToken ct)
    {
        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var count = await MyForums(dbCtx)
            .Where(f =>
                f.MessageCount
                > dbCtx
                    .GuildForumReadMarkers.Where(m =>
                        m.PlayerEntityId == PlayerId.Value && m.GuildEntityId == f.GuildEntityId
                    )
                    .Select(m => m.LastReadMessageId)
                    .FirstOrDefault()
            )
            .CountAsync(ct);

        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new UnreadForumsCountMessageComposer { UnreadForumsCount = count },
            ct
        );
    }

    public async Task SendForumsListAsync(
        int listCode,
        int startIndex,
        int amount,
        CancellationToken ct
    )
    {
        if (!Enum.IsDefined((GuildForumListType)listCode))
        {
            _logger.LogWarning(
                "Player {PlayerId} asked for unknown forum list {ListCode}",
                PlayerId,
                listCode
            );

            return;
        }

        var list = (GuildForumListType)listCode;
        var now = DateTime.UtcNow;
        var since = now.AddDays(-_config.ForumActivityDays);

        startIndex = Math.Max(0, startIndex);
        amount = Math.Clamp(amount, 0, _config.ForumPageSize);

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        // The Most lists are public forums only: ones everybody may read.
        var forums =
            list == GuildForumListType.MyForums
                ? MyForums(dbCtx)
                : dbCtx
                    .GuildForums.AsNoTracking()
                    .Where(x => x.ReadPermission == GuildForumPermission.Everybody);
        var total = await forums.CountAsync(ct);
        var ordered = list switch
        {
            GuildForumListType.MostActive => forums
                .OrderByDescending(f =>
                    dbCtx.GuildForumMessages.Count(m =>
                        m.GuildEntityId == f.GuildEntityId && m.CreatedAt >= since
                    )
                )
                .ThenByDescending(f => f.LastMessageAt),
            GuildForumListType.MostViewed => forums
                .OrderByDescending(f =>
                    dbCtx.GuildForumReadMarkers.Count(m =>
                        m.GuildEntityId == f.GuildEntityId && m.ReadAt >= since
                    )
                )
                .ThenByDescending(f => f.LastMessageAt),
            // Inference: no capture shows My Forums' order; the latest post first.
            _ => forums.OrderByDescending(f => f.LastMessageAt),
        };
        var guildIds = await ordered
            .ThenBy(f => f.GuildEntityId)
            .Skip(startIndex)
            .Take(amount)
            .Select(f => f.GuildEntityId)
            .ToListAsync(ct);

        await _grainFactory.SendComposerToPlayerAsync(
            PlayerId,
            new ForumsListMessageComposer
            {
                ListCode = list,
                TotalAmount = total,
                StartIndex = startIndex,
                Forums = await GuildForumQueries.ForumsAsync(
                    dbCtx,
                    guildIds,
                    PlayerId.Value,
                    since,
                    now,
                    ct
                ),
            },
            ct
        );
    }

    public async Task MarkReadAsync(
        ImmutableArray<GuildForumReadMarkerSnapshot> markers,
        CancellationToken ct
    )
    {
        if (markers.IsDefaultOrEmpty)
            return;

        var guildIds = markers.Select(x => x.GroupId).Distinct().ToList();
        var now = DateTime.UtcNow;

        await using var dbCtx = await _dbCtxFactory.CreateDbContextAsync(ct);

        var counts = await dbCtx
            .GuildForums.AsNoTracking()
            .Where(x => guildIds.Contains(x.GuildEntityId))
            .ToDictionaryAsync(x => x.GuildEntityId, x => x.MessageCount, ct);
        var existing = await dbCtx
            .GuildForumReadMarkers.Where(x =>
                x.PlayerEntityId == PlayerId.Value && guildIds.Contains(x.GuildEntityId)
            )
            .ToDictionaryAsync(x => x.GuildEntityId, ct);

        foreach (var marker in markers)
        {
            if (!counts.TryGetValue(marker.GroupId, out var messageCount))
                continue;

            // A marker never goes past the forum's last message, nor back.
            var read = marker.MarkAll
                ? messageCount
                : Math.Clamp(marker.LastReadMessageId, 0, messageCount);

            if (!existing.TryGetValue(marker.GroupId, out var row))
            {
                row = new GuildForumReadMarkerEntity
                {
                    PlayerEntityId = PlayerId.Value,
                    GuildEntityId = marker.GroupId,
                };
                dbCtx.GuildForumReadMarkers.Add(row);
                existing[marker.GroupId] = row;
            }

            row.LastReadMessageId = Math.Max(row.LastReadMessageId, read);
            row.ReadAt = now;
        }

        await dbCtx.SaveChangesAsync(ct);
    }

    /// <summary>The forums of the groups the player is a member of.</summary>
    private IQueryable<GuildForumEntity> MyForums(TurboDbContext dbCtx)
    {
        var ranks = GuildMemberRanks.MemberRanks();

        return dbCtx
            .GuildForums.AsNoTracking()
            .Where(f =>
                dbCtx.GuildMembers.Any(m =>
                    m.GuildEntityId == f.GuildEntityId
                    && m.PlayerEntityId == PlayerId.Value
                    && ranks.Contains(m.Rank)
                )
            );
    }
}
