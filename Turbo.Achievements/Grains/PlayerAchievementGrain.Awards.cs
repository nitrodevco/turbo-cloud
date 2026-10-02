using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Achievements;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Achievements.Grains;

internal sealed partial class PlayerAchievementGrain
{
    /// <summary>
    /// Opens an award for every level the progress newly qualifies for, freezing the definition
    /// revision it was earned under. Nothing is read from storage unless a level was actually
    /// crossed. Levels already earned are never opened again: <c>EarnedLevel</c> is the cursor.
    /// </summary>
    private static async Task CreateAwardsAsync(
        TurboDbContext db,
        AchievementDefinition definition,
        AchievementProgressEntity progress,
        DateTime earnedAt,
        CancellationToken ct
    )
    {
        var reached = progress.EarnedLevel;
        for (var index = progress.EarnedLevel; index < definition.Levels.Length; index++)
        {
            if (
                !AchievementReducerEngine.Qualifies(
                    definition,
                    definition.Levels[index],
                    progress.Value
                )
            )
                break;
            reached = index + 1;
        }
        if (reached <= progress.EarnedLevel)
            return;
        // Older catalogs recorded zero-valued HC state for non-members. A joining
        // level must also verify membership when retained progress is reconciled.
        if (
            definition.Source == AchievementSources.HC
            && definition.Levels[0].Requirement == 0
            && !await db.PlayerSubscriptions.AnyAsync(
                x =>
                    x.PlayerEntityId == progress.PlayerId
                    && x.SubscriptionType == SubscriptionType.HabboClub
                    && x.FirstSubscribedAt <= earnedAt,
                ct
            )
        )
            return;
        progress.WriteOpenAwards(
            progress
                .ReadOpenAwards()
                .Concat(
                    Enumerable
                        .Range(progress.EarnedLevel + 1, reached - progress.EarnedLevel)
                        .Select(level => new AchievementOpenAward
                        {
                            Level = level,
                            Revision = definition.Revision,
                            EarnedAtUtc = earnedAt,
                        })
                )
        );
        progress.EarnedLevel = reached;
    }

    private async Task DeliverAwardsAsync(CancellationToken ct)
    {
        await using var db = await _database.CreateDbContextAsync(ct);
        var pending = await db
            .AchievementProgress.Where(x =>
                x.PlayerId == _state.PlayerId.Value && x.PendingDelivery
            )
            .OrderBy(x => x.AchievementId)
            .ToListAsync(ct);
        var revisions = new Dictionary<(int, int), AchievementDefinition>();
        foreach (var progress in pending)
        foreach (var open in progress.ReadOpenAwards().Where(x => !x.Completed))
        {
            var awardKey = AchievementOpenAwards.AwardKey(
                progress.PlayerId,
                progress.AchievementId,
                open.Level
            );
            try
            {
                // A blocked award clears the tracker, which detaches the rows still to be visited.
                if (db.Entry(progress).State == EntityState.Detached)
                    db.Attach(progress);
                var definition = await ResolveRevisionAsync(
                    db,
                    progress.AchievementId,
                    open.Revision,
                    revisions,
                    ct
                );
                var level = definition.Levels[open.Level - 1];
                var delivered = open.DeliveredRewards;
                while (delivered < level.Rewards.Length)
                {
                    var reward = level.Rewards[delivered];
                    var receiptKey = $"{awardKey}:reward:{delivered}";
                    if (reward.Handler == "wallet" && reward.Version == 1)
                    {
                        if (
                            reward.Currency is not { } currency
                            || !await _grains
                                .GetPlayerWalletGrain(_state.PlayerId)
                                .CreditAchievementAsync(receiptKey, currency, reward.Amount, ct)
                        )
                            throw new InvalidOperationException(
                                "Wallet reward blocked: unknown currency or overflow."
                            );
                    }
                    else if (_rewards.TryGet(reward.Handler, reward.Version, out var handler))
                        await handler.DeliverAsync(_state.PlayerId, receiptKey, reward, ct);
                    else
                        throw new InvalidOperationException(
                            $"Reward handler unavailable: {reward.Handler}/{reward.Version}."
                        );
                    delivered++;
                    progress.ReplaceOpenAward(open with { DeliveredRewards = delivered });
                    await db.SaveChangesAsync(ct);
                }
                await _grains
                    .GetPlayerBadgeGrain(_state.PlayerId)
                    .GrantAchievementAsync(
                        progress.AchievementId,
                        open.Level,
                        level.BadgeCode,
                        ct
                    );
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                // The earned-level total moves by the levels this completion adds to the
                // achievement's highest completed level; the score by the frozen level score.
                var added = Math.Max(0, open.Level - progress.CompletedLevel);
                progress.CompletedLevel = Math.Max(progress.CompletedLevel, open.Level);
                progress.ScoreEarned = checked(progress.ScoreEarned + level.Score);
                progress.LastLevelAtUtc = DateTime.UtcNow;
                progress.ReplaceOpenAward(
                    open with
                    {
                        DeliveredRewards = delivered,
                        Completed = true,
                        BlockedReason = null,
                    }
                );
                var projection = await db.AchievementProjections.FindAsync(
                    [_state.PlayerId.Value],
                    ct
                );
                if (projection is null)
                {
                    projection = new() { PlayerId = _state.PlayerId.Value };
                    db.AchievementProjections.Add(projection);
                }
                projection.Score = checked(projection.Score + level.Score);
                projection.EarnedLevels = checked(projection.EarnedLevels + added);
                projection.PublicationPending = true;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Each achievement is ordered; unrelated awards continue after this group stops.
                _logger.LogError(ex, "Achievement award {AwardKey} blocked", awardKey);
                // A rollback can leave tracked changes ahead of the database.
                db.ChangeTracker.Clear();
                var blocked = await db.AchievementProgress.FindAsync(
                    [progress.PlayerId, progress.AchievementId],
                    ct
                );
                var stuck = blocked
                    ?.ReadOpenAwards()
                    .FirstOrDefault(x => x.Level == open.Level && !x.Completed);
                if (blocked is not null && stuck is not null)
                {
                    blocked.ReplaceOpenAward(
                        stuck with
                        {
                            BlockedReason = ex.Message.Length > 512 ? ex.Message[..512] : ex.Message,
                        }
                    );
                    await db.SaveChangesAsync(ct);
                }
                break;
            }
        }
    }

    /// <summary>
    /// The totals recomputed from every achievement's completed levels. Delivery keeps them
    /// incrementally; this is the check that runs whenever a player is evaluated against a changed
    /// catalog and after an operator's reconcile, so a drifted total cannot persist.
    /// </summary>
    private static async Task RecomputeTotalsAsync(
        TurboDbContext db,
        AchievementProjectionEntity projection,
        CancellationToken ct
    )
    {
        var completed = await db
            .AchievementProgress.AsNoTracking()
            .Where(x => x.PlayerId == projection.PlayerId)
            .Select(x => new { x.ScoreEarned, x.CompletedLevel })
            .ToListAsync(ct);
        var score = checked(completed.Sum(x => (long)x.ScoreEarned));
        var levels = completed.Sum(x => x.CompletedLevel);
        if (projection.Score == score && projection.EarnedLevels == levels)
            return;
        projection.Score = checked((int)score);
        projection.EarnedLevels = levels;
        projection.PublicationPending = true;
    }
}
