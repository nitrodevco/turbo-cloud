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
/// Who went into which room and when, from the entry log the navigator's history keeps: the room
/// visits log, narrowed to one player or one room, and the players who visited a room for its
/// page, newest first. The log grows with every room entered, so its pages are counted only so far
/// (<see cref="LogCounts"/>). Read-only.
/// </summary>
public sealed class AdminRoomVisits(
    IDbContextFactory<TurboDbContext> database,
    IOptions<AdminConfig> config
)
{
    /// <summary>The visits a room's page lists, newest first.</summary>
    public const int LIMIT = 50;

    /// <summary>
    /// The columns the log sorts by, each one its indexes order: when (the visit's id) and the
    /// room's id.
    /// </summary>
    private static readonly ListSorts<RoomEntryLogEntity> SORTS =
        new ListSorts<RoomEntryLogEntity>()
            .Add("when", ListOrder.DESC, x => x.Id)
            .Add("room", ListOrder.ASC, x => x.RoomEntityId, x => x.Id);

    /// <summary>
    /// A page of the log, narrowed to a player (by name or id) and a room when given, counted up
    /// to <see cref="LogCounts.LIMIT"/>.
    /// </summary>
    public async Task<RoomVisitLogResponse> SearchAsync(
        string? player,
        int? room,
        int page,
        string? sort,
        string? dir,
        CancellationToken ct
    )
    {
        var pageSize = Math.Max(1, config.Value.RoomVisitsPageSize);
        var order = SORTS.Resolve(sort, dir);
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
                return new RoomVisitLogResponse(0, false, 1, pageSize, order, []);

            visits = visits.Where(x => x.PlayerEntityId == found);
        }

        if (room is { } roomId)
            visits = visits.Where(x => x.RoomEntityId == roomId);

        var sorted = SORTS.Apply(visits, order);
        var (total, capped) = await LogCounts.CountAsync(sorted, ct).ConfigureAwait(false);

        page = LogCounts.Page(page, total, pageSize);

        var rows = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new RoomVisitLogEntry(
                x.Id,
                x.CreatedAt,
                x.PlayerEntityId,
                x.PlayerEntity != null ? x.PlayerEntity.Name : null,
                x.RoomEntityId,
                x.RoomEntity != null ? x.RoomEntity.Name : null
            ))
            .ToArrayAsync(ct)
            .ConfigureAwait(false);

        return new RoomVisitLogResponse(total, capped, page, pageSize, order, rows);
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
}
