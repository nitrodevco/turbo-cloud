using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Messages.Outgoing.Inventory.Achievements;
using Turbo.Primitives.Messages.Outgoing.Notifications;
using Turbo.Primitives.Orleans;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Achievements.Grains;

internal sealed partial class PlayerAchievementGrain
{
    private async Task PublishAsync(CancellationToken ct)
    {
        await using var db = await _database.CreateDbContextAsync(ct);
        var projection = await db.AchievementProjections.FindAsync([_state.PlayerId.Value], ct);
        if (projection is not null && projection.PublicationPending)
        {
            await _grains
                .GetPlayerGrain(_state.PlayerId)
                .SetAchievementTotalsAsync(projection.Score, projection.EarnedLevels, ct);
            await _grains.SendComposerToPlayerAsync(
                _state.PlayerId,
                new AchievementsScoreEventMessageComposer { Score = projection.Score },
                ct
            );
            projection.PublicationPending = false;
            await db.SaveChangesAsync(ct);
        }
        var presentations = await db
            .AchievementAwards.Where(x =>
                x.PlayerId == _state.PlayerId.Value && x.Completed && !x.Presented
            )
            .OrderBy(x => x.AchievementId)
            .ThenBy(x => x.Level)
            .ToListAsync(ct);
        foreach (var award in presentations)
        {
            var definition = JsonSerializer.Deserialize<AchievementDefinition>(
                award.DefinitionJson
            )!;
            var level = JsonSerializer.Deserialize<AchievementLevelDefinition>(award.RewardJson)!;
            var badge = await _grains
                .GetPlayerBadgeGrain(_state.PlayerId)
                .GetBadgeSnapshotAsync(level.BadgeCode, ct);
            var activity = level.Rewards.FirstOrDefault(x =>
                x.Currency?.CurrencyType == CurrencyType.ActivityPoints
            );
            var delivered = await _grains.TrySendComposerToPlayerAsync(
                _state.PlayerId,
                new HabboAchievementNotificationMessageComposer
                {
                    Type = 1,
                    Level = award.Level,
                    BadgeId = badge?.BadgeId ?? 0,
                    BadgeCode = level.BadgeCode,
                    PointsTotal = definition.Levels.Take(award.Level).Sum(x => x.Score),
                    LevelRewardPoints = activity?.Amount ?? 0,
                    LevelRewardPointType = activity?.Currency?.ActivityPointType ?? 0,
                    BonusPoints = 0,
                    AchievementId = award.AchievementId,
                    RemovedBadgeCode =
                        award.Level > 1 ? definition.Levels[award.Level - 2].BadgeCode : "",
                    Category = definition.Category,
                    ShowDialogToUser = _config.ShowCongratulationsDialog,
                    OwnerCount = badge?.OwnerCount ?? 0,
                    BadgeRarityId = badge is null ? 0 : (int)badge.Rarity,
                },
                ct
            );
            if (!delivered)
                break;
            var progress = await db.AchievementProgress.FindAsync(
                [_state.PlayerId.Value, award.AchievementId],
                ct
            );
            await _grains.SendComposerToPlayerAsync(
                _state.PlayerId,
                new AchievementEventMessageComposer
                {
                    Achievement = AchievementProjection.ToSnapshot(
                        _catalog.Current.FirstOrDefault(x => x.Id == award.AchievementId)
                            ?? definition,
                        progress
                    ),
                },
                ct
            );
            award.Presented = true;
            await db.SaveChangesAsync(ct);
        }
    }
}
