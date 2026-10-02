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

    public static async Task RecordOwnedStateAsync(
        TurboDbContext db,
        IAchievementFactRecorder recorder,
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
        recorder.Record(
            db,
            playerId,
            new AchievementFact
            {
                OperationId = $"state:floor-heights:{Guid.NewGuid():N}",
                Source = AchievementSources.FLOOR_HEIGHTS,
                OccurredAtUtc = DateTime.UtcNow,
                Amount = models.Select(CountFloorHeights).DefaultIfEmpty(0).Max(),
            }
        );
        var ranked = await db
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
        var rank = ranked.FindIndex(x => x == playerId.Value);
        if (rank >= 0)
            recorder.Record(
                db,
                playerId,
                new AchievementFact
                {
                    OperationId = $"state:room-rank:{Guid.NewGuid():N}",
                    Source = AchievementSources.ROOM_RANK,
                    OccurredAtUtc = DateTime.UtcNow,
                    Amount = rank + 1,
                }
            );
    }
}
