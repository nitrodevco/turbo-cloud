using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Context;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Achievements;

/// <summary>Current room state, using Navigator's public top-rated eligibility and ordering.</summary>
public static class AchievementRoomCriteria
{
    public static int CountFloorHeights(string model) =>
        model
            .ToLowerInvariant()
            .Where(c => c != 'x' && "0123456789abcdefghijklmnopqrstuvwxyz".Contains(c))
            .Distinct()
            .Count();

    public static async Task RecordRankingsAsync(
        TurboDbContext db,
        IAchievementFactRecorder recorder,
        CancellationToken ct
    )
    {
        var rooms = await db
            .Rooms.AsNoTracking()
            .Where(x =>
                x.DeletedAt == null
                && !x.HiddenByBc
                && x.DoorMode != RoomDoorModeType.Invisible
                && x.Score > 0
            )
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Id)
            .Select(x => x.PlayerEntityId)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;
        for (var index = 0; index < rooms.Count; index++)
            recorder.Record(
                db,
                rooms[index],
                new AchievementFact
                {
                    OperationId = $"room-rank:{Guid.NewGuid():N}",
                    Source = AchievementSources.ROOM_RANK,
                    OccurredAtUtc = now,
                    Amount = index + 1,
                }
            );
    }

    /// <summary>The current floor-height count of a player's rooms and the standing of their best room.</summary>
    public readonly record struct OwnedState(long FloorHeights, long? Rank);

    /// <summary>
    /// Standing is one plus the eligible rooms ahead of the player's best room, so two cheap
    /// queries answer it rather than reading every ranked room.
    /// </summary>
    public static async Task<OwnedState> ReadOwnedStateAsync(
        TurboDbContext db,
        PlayerId playerId,
        CancellationToken ct
    )
    {
        var models = await (
            from room in db.Rooms.AsNoTracking()
            join model in db.RoomModels.AsNoTracking() on room.RoomModelEntityId equals model.Id
            where room.PlayerEntityId == playerId.Value && room.DeletedAt == null
            select model.Model
        )
            .ToListAsync(ct)
            .ConfigureAwait(false);
        var eligible = db
            .Rooms.AsNoTracking()
            .Where(x =>
                x.DeletedAt == null
                && !x.HiddenByBc
                && x.DoorMode != RoomDoorModeType.Invisible
                && x.Score > 0
            );
        var best = await eligible
            .Where(x => x.PlayerEntityId == playerId.Value)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Id)
            .Select(x => new { x.Score, x.Id })
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);
        long? rank = null;
        if (best is not null)
            rank =
                1
                + await eligible
                    .CountAsync(
                        x => x.Score > best.Score || (x.Score == best.Score && x.Id > best.Id),
                        ct
                    )
                    .ConfigureAwait(false);
        return new(models.Select(CountFloorHeights).DefaultIfEmpty(0).Max(), rank);
    }
}
