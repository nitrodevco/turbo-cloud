using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Admin.Api.Contracts;
using Turbo.Database.Context;
using Turbo.Database.Entities.Room;

namespace Turbo.Admin.Rooms;

/// <summary>
/// Who went into which room and when, from the entry log the navigator's history keeps: the rooms
/// a player visited, and the players who visited a room, newest first. Read-only.
/// </summary>
public sealed class AdminRoomVisits(IDbContextFactory<TurboDbContext> database)
{
    /// <summary>The visits a page lists, newest first.</summary>
    public const int LIMIT = 50;

    /// <summary>The rooms a player went into, newest first.</summary>
    public Task<IReadOnlyList<RoomVisitItem>> ForPlayerAsync(int playerId, CancellationToken ct) =>
        ListAsync(x => x.PlayerEntityId == playerId, ct);

    /// <summary>The players who went into a room, newest first.</summary>
    public Task<IReadOnlyList<RoomVisitItem>> ForRoomAsync(int roomId, CancellationToken ct) =>
        ListAsync(x => x.RoomEntityId == roomId, ct);

    private async Task<IReadOnlyList<RoomVisitItem>> ListAsync(
        Expression<Func<RoomEntryLogEntity, bool>> filter,
        CancellationToken ct
    )
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);

        return await db
            .RoomEntryLogs.AsNoTracking()
            .Where(filter)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Take(LIMIT)
            .Select(x => new RoomVisitItem(
                x.PlayerEntityId,
                // The log outlives the player and the room, so either can be gone.
                db.Players.Where(p => p.Id == x.PlayerEntityId)
                    .Select(p => p.Name)
                    .FirstOrDefault()
                    ?? "",
                x.RoomEntityId,
                db.Rooms.Where(r => r.Id == x.RoomEntityId).Select(r => r.Name).FirstOrDefault()
                    ?? "",
                x.CreatedAt
            ))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }
}
