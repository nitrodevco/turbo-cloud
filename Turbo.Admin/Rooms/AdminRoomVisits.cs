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
using Turbo.Primitives.Players;

namespace Turbo.Admin.Rooms;

/// <summary>
/// Who went into which room and when, from the entry log the navigator's history keeps: the room
/// visits log, narrowed to one player or one room, and the players who visited a room for its
/// page, newest first. The log is paged by visit id rather than by page number and count: it grows
/// with every room entered, and a count would read all of it for each page. Read-only.
/// </summary>
public sealed class AdminRoomVisits(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config
)
{
    /// <summary>The visits a room's page lists, newest first.</summary>
    public const int LIMIT = 50;

    /// <summary>
    /// A run of the log, narrowed to a player (by name or id) and a room when given.
    /// <paramref name="before"/> asks for the visits older than that one, <paramref name="after"/>
    /// for those newer; with neither, the newest.
    /// </summary>
    public async Task<RoomVisitLogResponse> SearchAsync(
        string? player,
        int? room,
        int? before,
        int? after,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.RoomVisitsPageSize);
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        var visits = db.RoomEntryLogs.AsNoTracking();
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
                return new RoomVisitLogResponse(pageSize, false, false, []);

            visits = visits.Where(x => x.PlayerEntityId == found);
        }

        if (room is { } roomId)
            visits = visits.Where(x => x.RoomEntityId == roomId);

        List<RoomVisitLogEntry> rows;
        bool hasOlder;
        bool hasNewer;

        if (after is { } newerThan)
        {
            rows = await Entries(visits.Where(x => x.Id > newerThan).OrderBy(x => x.Id), pageSize)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            hasNewer = rows.Count > pageSize;
            rows = [.. rows.Take(pageSize).Reverse()];
            hasOlder = await visits.AnyAsync(x => x.Id <= newerThan, ct).ConfigureAwait(false);
        }
        else
        {
            var page = before is { } olderThan ? visits.Where(x => x.Id < olderThan) : visits;

            rows = await Entries(page.OrderByDescending(x => x.Id), pageSize)
                .ToListAsync(ct)
                .ConfigureAwait(false);
            hasOlder = rows.Count > pageSize;
            rows = [.. rows.Take(pageSize)];
            hasNewer =
                before is { } newest
                && await visits.AnyAsync(x => x.Id >= newest, ct).ConfigureAwait(false);
        }

        return new RoomVisitLogResponse(pageSize, hasOlder, hasNewer, [.. rows]);
    }

    /// <summary>The players who went into a room, newest first.</summary>
    public async Task<IReadOnlyList<RoomVisitItem>> ForRoomAsync(int roomId, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db
            .RoomEntryLogs.AsNoTracking()
            .Where(x => x.RoomEntityId == roomId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(LIMIT)
            .Select(x => new RoomVisitItem(
                x.PlayerEntityId,
                x.PlayerEntity != null ? x.PlayerEntity.Name : "",
                x.RoomEntityId,
                x.RoomEntity != null ? x.RoomEntity.Name : "",
                x.CreatedAt
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// A page of visits in the order given, one more than a page to know whether there are more,
    /// with the names of their players and rooms.
    /// </summary>
    private static IQueryable<RoomVisitLogEntry> Entries(
        IOrderedQueryable<RoomEntryLogEntity> visits,
        int pageSize
    ) =>
        visits
            .Take(pageSize + 1)
            .Select(x => new RoomVisitLogEntry(
                x.Id,
                x.CreatedAt,
                x.PlayerEntityId,
                x.PlayerEntity != null ? x.PlayerEntity.Name : null,
                x.RoomEntityId,
                x.RoomEntity != null ? x.RoomEntity.Name : null
            ));
}
