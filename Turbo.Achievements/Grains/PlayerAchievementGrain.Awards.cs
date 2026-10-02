using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Turbo.Database.Context;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Achievements.Grains;

internal sealed partial class PlayerAchievementGrain
{
    /// <summary>
    /// Freezes an award for every level the progress newly qualifies for. Nothing is read from
    /// storage unless a level was actually crossed, and then it is one query for the player's
    /// existing award levels of that achievement.
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
        var existing = (
            await db
                .AchievementAwards.Where(x =>
                    x.PlayerId == progress.PlayerId && x.AchievementId == definition.Id
                )
                .Select(x => x.Level)
                .ToListAsync(ct)
        ).ToHashSet();
        var definitionJson = JsonSerializer.Serialize(definition);
        for (var level = progress.EarnedLevel + 1; level <= reached; level++)
            if (existing.Add(level))
                db.AchievementAwards.Add(
                    new()
                    {
                        PlayerId = progress.PlayerId,
                        AchievementId = definition.Id,
                        Level = level,
                        Revision = definition.Revision,
                        AwardKey = $"achievement:{progress.PlayerId}:{definition.Id}:{level}",
                        DefinitionJson = definitionJson,
                        RewardJson = JsonSerializer.Serialize(definition.Levels[level - 1]),
                        EarnedAtUtc = earnedAt,
                    }
                );
        progress.EarnedLevel = reached;
    }

    private async Task DeliverAwardsAsync(CancellationToken ct)
    {
        await using var db = await _database.CreateDbContextAsync(ct);
        var pending = await db
            .AchievementAwards.Where(x => x.PlayerId == _state.PlayerId.Value && !x.Completed)
            .OrderBy(x => x.AchievementId)
            .ThenBy(x => x.Level)
            .ToListAsync(ct);
        foreach (var achievement in pending.GroupBy(x => x.AchievementId))
        foreach (var award in achievement)
        {
            try
            {
                if (db.Entry(award).State == EntityState.Detached)
                    db.Attach(award);
                var level =
                    JsonSerializer.Deserialize<AchievementLevelDefinition>(award.RewardJson)
                    ?? throw new InvalidOperationException("Missing frozen reward.");
                while (award.DeliveredRewards < level.Rewards.Length)
                {
                    var reward = level.Rewards[award.DeliveredRewards];
                    var receiptKey = $"{award.AwardKey}:reward:{award.DeliveredRewards}";
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
                    award.DeliveredRewards++;
                    await db.SaveChangesAsync(ct);
                }
                await _grains
                    .GetPlayerBadgeGrain(_state.PlayerId)
                    .GrantAchievementAsync(award.AchievementId, award.Level, level.BadgeCode, ct);
                // The earned-level total moves by the levels this completion adds to the
                // achievement's highest completed level; the score by the frozen level score.
                var highest =
                    await db
                        .AchievementAwards.Where(x =>
                            x.PlayerId == award.PlayerId
                            && x.AchievementId == award.AchievementId
                            && x.Completed
                        )
                        .MaxAsync(x => (int?)x.Level, ct)
                    ?? 0;
                await using var tx = await db.Database.BeginTransactionAsync(ct);
                award.Completed = true;
                award.BlockedReason = null;
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
                projection.EarnedLevels = checked(
                    projection.EarnedLevels + Math.Max(0, award.Level - highest)
                );
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
                _logger.LogError(ex, "Achievement award {AwardKey} blocked", award.AwardKey);
                // A rollback can leave tracked changes ahead of the database.
                db.ChangeTracker.Clear();
                var blocked = await db.AchievementAwards.FindAsync(
                    [award.PlayerId, award.AchievementId, award.Level],
                    ct
                );
                if (blocked is not null && !blocked.Completed)
                {
                    blocked.BlockedReason =
                        ex.Message.Length > 512 ? ex.Message[..512] : ex.Message;
                    await db.SaveChangesAsync(ct);
                }
                break;
            }
        }
    }

    /// <summary>
    /// The totals recomputed from every completed award. Delivery keeps them incrementally; this
    /// is the check that runs whenever a player is evaluated against a changed catalog and after
    /// an operator's reconcile, so a drifted total cannot persist.
    /// </summary>
    private static async Task RecomputeTotalsAsync(
        TurboDbContext db,
        AchievementProjectionEntity projection,
        CancellationToken ct
    )
    {
        var completed = await db
            .AchievementAwards.AsNoTracking()
            .Where(x => x.PlayerId == projection.PlayerId && x.Completed)
            .Select(x => new
            {
                x.AchievementId,
                x.Level,
                x.RewardJson,
            })
            .ToListAsync(ct);
        var score = checked(
            completed.Sum(x =>
                (long)JsonSerializer.Deserialize<AchievementLevelDefinition>(x.RewardJson)!.Score
            )
        );
        var levels = completed.GroupBy(x => x.AchievementId).Sum(x => x.Max(a => a.Level));
        if (projection.Score == score && projection.EarnedLevels == levels)
            return;
        projection.Score = checked((int)score);
        projection.EarnedLevels = levels;
        projection.PublicationPending = true;
    }
}
