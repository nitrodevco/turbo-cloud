using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Players;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Achievements;

/// <summary>
/// Authoritative state criteria only. Action counts are never inferred from unrelated state.
/// A source is recorded as a fact only when its value moved since the last one, so a login that
/// changes nothing writes nothing; <c>force</c> records every source again, for a catalog whose
/// new definitions have not seen the current values yet.
/// </summary>
public sealed class AchievementStateEvaluator(
    IDbContextFactory<TurboDbContext> database,
    IAchievementFactRecorder recorder
)
{
    public async Task RecordAsync(PlayerId playerId, CancellationToken ct, bool force = false)
    {
        var db = await database.CreateDbContextAsync(ct).ConfigureAwait(false);
        await using var dbScope = db.ConfigureAwait(false);
        var player = await db
            .Players.AsNoTracking()
            .SingleAsync(x => x.Id == playerId.Value, ct)
            .ConfigureAwait(false);
        var stored = await db
            .AchievementStateValues.Where(x => x.PlayerId == playerId.Value)
            .ToDictionaryAsync(x => x.Source, ct)
            .ConfigureAwait(false);
        var now = DateTime.UtcNow;
        void Record(string source, long value)
        {
            if (stored.TryGetValue(source, out var last))
            {
                if (!force && last.Value == value)
                    return;
                last.Value = value;
            }
            else
                db.AchievementStateValues.Add(
                    new()
                    {
                        PlayerId = playerId.Value,
                        Source = source,
                        Value = value,
                    }
                );
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
        }
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
        var club = await db
            .PlayerSubscriptions.AsNoTracking()
            .SingleOrDefaultAsync(
                x =>
                    x.PlayerEntityId == playerId.Value
                    && x.SubscriptionType == SubscriptionType.HabboClub,
                ct
            )
            .ConfigureAwait(false);
        Record(AchievementSources.PURCHASED_HC, club?.PurchasedDaysSubscribed ?? 0);
        // A zero-threshold membership award requires an actual eligible interval.
        // Recording zero for non-members would manufacture a joining event.
        if (club is { FirstSubscribedAt: not null, ExpiresAt: { } expiresAt })
            Record(AchievementSources.HC, EligibleSeconds(club.TotalDaysSubscribed, expiresAt, now));
        var rooms = await AchievementRoomCriteria
            .ReadOwnedStateAsync(db, playerId, ct)
            .ConfigureAwait(false);
        Record(AchievementSources.FLOOR_HEIGHTS, rooms.FloorHeights);
        if (rooms.Rank is { } rank)
            Record(AchievementSources.ROOM_RANK, rank);
        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Seconds actually spent as a member. Every grant covers exactly its days without overlap
    /// (an active membership is extended from its end, a lapsed one from now), so the days ever
    /// granted minus what is left of the current run have all elapsed.
    /// </summary>
    public static long EligibleSeconds(int totalDaysGranted, DateTime expiresAt, DateTime now)
    {
        var granted = (long)TimeSpan.FromDays(totalDaysGranted).TotalSeconds;
        var remaining = expiresAt > now ? (long)(expiresAt - now).TotalSeconds : 0;

        return Math.Max(0, granted - remaining);
    }
}
