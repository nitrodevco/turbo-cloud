using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players;

namespace Turbo.Achievements;

/// <summary>Authoritative state criteria only. Action counts are never inferred from unrelated state.</summary>
public sealed class AchievementStateEvaluator(
    IDbContextFactory<TurboDbContext> database,
    IAchievementFactRecorder recorder
)
{
    public async Task RecordAsync(PlayerId playerId, CancellationToken ct)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);
        var player = await db
            .Players.AsNoTracking()
            .SingleAsync(x => x.Id == playerId.Value, ct)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;
        void Record(string source, long value) =>
            recorder.Record(
                db,
                playerId,
                new()
                {
                    OperationId = $"state:{source}:{Guid.NewGuid():N}",
                    Source = source,
                    Amount = value,
                    OccurredAtUtc = now,
                }
            );
        Record(
            AchievementSources.ACCOUNT_AGE,
            Math.Max(0, (long)(now - player.CreatedAt).TotalDays)
        );
        Record(
            AchievementSources.PETS,
            await db
                .Pets.CountAsync(x => x.PlayerEntityId == playerId.Value && x.DeletedAt == null, ct)
                .ConfigureAwait(false)
        );
        // Actual purchased intervals, rather than the subscription's aggregate grant counter.
        var memberships = await db
            .AchievementMembershipIntervals.AsNoTracking()
            .Where(x => x.PlayerId == playerId.Value)
            .ToListAsync(ct)
            .ConfigureAwait(false);
        Record(
            AchievementSources.PURCHASED_HC,
            memberships.Where(x => x.Purchased).Sum(x => (long)(x.EndUtc - x.StartUtc).TotalDays)
        );
        var eligible = memberships.Where(x => x.StartUtc < now).OrderBy(x => x.StartUtc).ToList();
        long ticks = 0;
        DateTime? through = null;
        foreach (var interval in eligible)
        {
            var start = through is { } last && last > interval.StartUtc ? last : interval.StartUtc;
            var end = interval.EndUtc > now ? now : interval.EndUtc;
            if (end > start)
                ticks = checked(ticks + (end - start).Ticks);
            if (through is null || end > through)
                through = end;
        }
        Record(AchievementSources.HC, ticks / TimeSpan.TicksPerSecond);
        await AchievementRoomCriteria
            .RecordOwnedStateAsync(db, recorder, playerId, ct)
            .ConfigureAwait(false);
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }
}
