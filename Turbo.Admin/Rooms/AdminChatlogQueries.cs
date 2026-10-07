using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;

namespace Turbo.Admin.Rooms;

/// <summary>
/// The room chat log as staff read it: newest first, narrowed to one player (what they said and
/// what was whispered to them), one room, or words in the line, or one line in context, the lines
/// around it in its room. Paged by line id rather than by page number and count: the log grows
/// with every word said, and a count would read all of it for each page. Read-only.
/// </summary>
public sealed class AdminChatlogQueries(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config
)
{
    private const string ESCAPE = "\\";

    /// <summary>
    /// A run of the log. <paramref name="before"/> asks for the lines older than that one,
    /// <paramref name="after"/> for those newer; with neither, the newest.
    /// </summary>
    public async Task<ChatlogResponse> SearchAsync(
        string? player,
        int? room,
        string? text,
        int? before,
        int? after,
        CancellationToken ct
    )
    {
        var pageSize = PageSize;
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var lines = db.Chatlogs.AsNoTracking();
        var who = (player ?? string.Empty).Trim();

        if (who.Length > 0)
        {
            int? playerId = int.TryParse(who, out var id)
                ? id
                : await db
                    .Players.AsNoTracking()
                    .Where(x => x.Name == who)
                    .Select(x => (int?)x.Id)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

            if (playerId is not { } found)
                return new ChatlogResponse(pageSize, false, false, []);

            lines = lines.Where(x => x.PlayerEntityId == found || x.TargetPlayerEntityId == found);
        }

        if (room is { } roomId)
            lines = lines.Where(x => x.RoomEntityId == roomId);

        var words = (text ?? string.Empty).Trim();

        if (words.Length > RoomChatlogEntity.MESSAGE_MAX_LENGTH)
            words = words[..RoomChatlogEntity.MESSAGE_MAX_LENGTH];

        if (words.Length > 0)
        {
            // LIKE, so case is ignored the same way on every database; the text is escaped so a
            // % or _ in it is matched as itself.
            var like = words
                .Replace(ESCAPE, ESCAPE + ESCAPE, StringComparison.Ordinal)
                .Replace("%", ESCAPE + "%", StringComparison.Ordinal)
                .Replace("_", ESCAPE + "_", StringComparison.Ordinal);

            lines = lines.Where(x => EF.Functions.Like(x.Message, "%" + like + "%", ESCAPE));
        }

        List<RoomChatlogEntity> rows;
        bool hasOlder;
        bool hasNewer;

        if (after is { } newerThan)
        {
            rows = await lines
                .Where(x => x.Id > newerThan)
                .OrderBy(x => x.Id)
                .Take(pageSize + 1)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            hasNewer = rows.Count > pageSize;
            rows = [.. rows.Take(pageSize).Reverse()];
            hasOlder = await lines.AnyAsync(x => x.Id <= newerThan, ct).ConfigureAwait(false);
        }
        else
        {
            var page = before is { } olderThan ? lines.Where(x => x.Id < olderThan) : lines;

            rows = await page.OrderByDescending(x => x.Id)
                .Take(pageSize + 1)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            hasOlder = rows.Count > pageSize;
            rows = [.. rows.Take(pageSize)];
            hasNewer =
                before is { } newest
                && await lines.AnyAsync(x => x.Id >= newest, ct).ConfigureAwait(false);
        }

        return new ChatlogResponse(
            pageSize,
            hasOlder,
            hasNewer,
            await NamedAsync(db, rows, ct).ConfigureAwait(false)
        );
    }

    /// <summary>
    /// One line in context: the lines said around it in its room, before and after, newest first.
    /// Empty when there is no such line.
    /// </summary>
    public async Task<ChatlogResponse> AroundAsync(int lineId, CancellationToken ct)
    {
        var side = Math.Max(1, PageSize / 2);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var roomId = await db
            .Chatlogs.AsNoTracking()
            .Where(x => x.Id == lineId)
            .Select(x => (int?)x.RoomEntityId)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (roomId is not { } room)
            return new ChatlogResponse(PageSize, false, false, []);

        var inRoom = db.Chatlogs.AsNoTracking().Where(x => x.RoomEntityId == room);
        // The line itself and the side before it, and one more to know whether there is more.
        var older = await inRoom
            .Where(x => x.Id <= lineId)
            .OrderByDescending(x => x.Id)
            .Take(side + 2)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var newer = await inRoom
            .Where(x => x.Id > lineId)
            .OrderBy(x => x.Id)
            .Take(side + 1)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        List<RoomChatlogEntity> rows = [.. newer.Take(side).Reverse(), .. older.Take(side + 1)];

        return new ChatlogResponse(
            PageSize,
            older.Count > side + 1,
            newer.Count > side,
            await NamedAsync(db, rows, ct).ConfigureAwait(false)
        );
    }

    private int PageSize => Math.Max(1, config.Value.ChatlogPageSize);

    /// <summary>The lines with the names of the players and rooms on them, in one query each.</summary>
    private static async Task<ChatlogEntry[]> NamedAsync(
        TurboDbContext db,
        List<RoomChatlogEntity> rows,
        CancellationToken ct
    )
    {
        var playerIds = rows.Select(x => x.PlayerEntityId)
            .Concat(rows.Select(x => x.TargetPlayerEntityId).OfType<int>())
            .Distinct()
            .ToList();
        var roomIds = rows.Select(x => x.RoomEntityId).Distinct().ToList();
        var players = await db
            .Players.AsNoTracking()
            .Where(x => playerIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);
        var rooms = await db
            .Rooms.AsNoTracking()
            .Where(x => roomIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct)
            .ConfigureAwait(false);

        return
        [
            .. rows.Select(x => new ChatlogEntry(
                x.Id,
                x.CreatedAt,
                x.PlayerEntityId,
                players.GetValueOrDefault(x.PlayerEntityId),
                x.RoomEntityId,
                rooms.GetValueOrDefault(x.RoomEntityId),
                x.TargetPlayerEntityId,
                x.TargetPlayerEntityId is { } target ? players.GetValueOrDefault(target) : null,
                x.Message
            )),
        ];
    }
}
