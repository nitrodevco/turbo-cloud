using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Turbo.Admin.Api;
using Turbo.Admin.Api.Contracts;
using Turbo.Admin.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Players;

namespace Turbo.Admin.Rooms;

/// <summary>
/// The room chat log as staff read it: newest first, narrowed to one player (what they said and
/// what was whispered to them), one room, or words in the line, or one line in context, the lines
/// around it in its room. The log grows with every word said, so its pages are counted only so far
/// (<see cref="LogCounts"/>) and it sorts only by when. Read-only.
/// </summary>
public sealed class AdminChatlogQueries(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config
)
{
    private const string ESCAPE = "\\";

    /// <summary>
    /// The columns the log sorts by: when, the line's id, which the table's key orders already.
    /// Names, rooms and words would sort the whole log for each page.
    /// </summary>
    private static readonly ListSorts<RoomChatlogEntity> SORTS =
        new ListSorts<RoomChatlogEntity>().Add("when", ListOrder.DESC, x => x.Id);

    /// <summary>A page of the log, counted up to <see cref="LogCounts.LIMIT"/>.</summary>
    public async Task<ChatlogResponse> SearchAsync(
        string? player,
        int? room,
        string? text,
        int page,
        string? sort,
        string? dir,
        CancellationToken ct
    )
    {
        var pageSize = PageSize;
        var order = SORTS.Resolve(sort, dir);
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
                    .Where(x => x.NameKey == PlayerNames.Key(who))
                    .Select(x => (int?)x.Id)
                    .FirstOrDefaultAsync(ct)
                    .ConfigureAwait(false);

            if (playerId is not { } found)
                return new ChatlogResponse(0, false, 1, pageSize, order, []);

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

        var sorted = SORTS.Apply(lines, order);
        var (total, capped) = await LogCounts.CountAsync(sorted, ct).ConfigureAwait(false);

        page = LogCounts.Page(page, total, pageSize);

        var rows = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new ChatlogResponse(
            total,
            capped,
            page,
            pageSize,
            order,
            await NamedAsync(db, rows, ct).ConfigureAwait(false)
        );
    }

    /// <summary>
    /// One line in context: the lines said around it in its room, before and after, newest first.
    /// Empty when there is no such line.
    /// </summary>
    public async Task<ChatlogContextResponse> AroundAsync(int lineId, CancellationToken ct)
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
            return new ChatlogContextResponse(false, false, []);

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

        return new ChatlogContextResponse(
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
