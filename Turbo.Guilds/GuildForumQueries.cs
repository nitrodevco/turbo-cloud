using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Database.Extensions;
using Turbo.Primitives.Guilds.Forums.Snapshots;

namespace Turbo.Guilds;

/// <summary>The forum rows both forum grains send: one forum, or a page of a list.</summary>
internal static class GuildForumQueries
{
    /// <summary>
    /// The forums of <paramref name="guildIds"/> as <paramref name="viewerId"/> sees them, in the
    /// order of the ids: name, badge, counts, the viewer's unread messages and the posts since
    /// <paramref name="since"/>. Ids without a forum are left out.
    /// </summary>
    public static async Task<ImmutableArray<GuildForumSnapshot>> ForumsAsync(
        TurboDbContext dbCtx,
        IReadOnlyList<int> guildIds,
        int viewerId,
        DateTime since,
        DateTime now,
        CancellationToken ct
    )
    {
        if (guildIds.Count == 0)
            return [];

        var rows = await dbCtx
            .GuildForums.AsNoTracking()
            .Where(x => guildIds.Contains(x.GuildEntityId))
            .Join(
                dbCtx.Guilds.AsNoTracking(),
                f => f.GuildEntityId,
                g => g.Id,
                (f, g) => new { f, g }
            )
            .ToListAsync(ct);
        var authorIds = rows.Select(x => x.f.LastMessagePlayerEntityId ?? 0)
            .Where(x => x > 0)
            .Distinct()
            .ToList();
        var names = await dbCtx
            .Players.AsNoTracking()
            .Where(x => authorIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var markers = await dbCtx
            .GuildForumReadMarkers.AsNoTracking()
            .Where(x => x.PlayerEntityId == viewerId && guildIds.Contains(x.GuildEntityId))
            .ToDictionaryAsync(x => x.GuildEntityId, x => x.LastReadMessageId, ct);
        var recent = await dbCtx
            .GuildForumMessages.AsNoTracking()
            .Where(x => guildIds.Contains(x.GuildEntityId) && x.CreatedAt >= since)
            .GroupBy(x => x.GuildEntityId)
            .Select(x => new { GuildId = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.GuildId, x => x.Count, ct);
        var byGuild = rows.ToDictionary(x => x.g.Id);

        return
        [
            .. guildIds
                .Where(byGuild.ContainsKey)
                .Select(id =>
                {
                    var row = byGuild[id];

                    return row.f.ToSnapshot(
                        row.g,
                        names.GetValueOrDefault(row.f.LastMessagePlayerEntityId ?? 0, ""),
                        markers.GetValueOrDefault(id),
                        recent.GetValueOrDefault(id),
                        now
                    );
                }),
        ];
    }
}
