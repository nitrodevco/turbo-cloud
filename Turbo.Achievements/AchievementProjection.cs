using System;
using Turbo.Database.Entities.Achievements;
using Turbo.Primitives.Achievements;
using Turbo.Primitives.Achievements.Snapshots;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Achievements;

public static class AchievementProjection
{
    public static AchievementSnapshot ToSnapshot(
        AchievementDefinition definition,
        AchievementProgressEntity? progress
    )
    {
        ArgumentNullException.ThrowIfNull(definition);
        var earned = Math.Clamp(progress?.EarnedLevel ?? 0, 0, definition.Levels.Length);
        var final = earned == definition.Levels.Length;
        var target = Math.Min(earned + 1, definition.Levels.Length);
        var level = definition.Levels[target - 1];
        var start = target > 1 ? definition.Levels[target - 2].Requirement : 0;
        var raw = Math.Min(
            int.MaxValue,
            Math.Max(0, (progress?.Value ?? 0) / definition.UnitDivisor)
        );
        var activity = System.Linq.Enumerable.FirstOrDefault(
            level.Rewards,
            x => x.Currency?.CurrencyType == CurrencyType.ActivityPoints
        );
        return new()
        {
            AchievementId = definition.Id,
            Level = target,
            BadgeId = level.BadgeCode,
            ScoreAtStartOfLevel =
                definition.Reducer == Turbo.Primitives.Achievements.Enums.AchievementReducer.Rank
                    ? 0
                    : start,
            ScoreLimitTotal = level.Requirement,
            CurrentPointsTotal = (int)raw,
            LevelRewardPoints = activity?.Amount ?? 0,
            LevelRewardPointType = activity?.Currency?.ActivityPointType ?? 0,
            FinalLevel = final,
            Category = definition.Category,
            SubCategory = definition.SubCategory,
            LevelCount = definition.Levels.Length,
            DisplayMethod = definition.DisplayMethod,
            State =
                definition.Archived ? Turbo.Primitives.Achievements.Enums.AchievementState.Archived
                : definition.Enabled ? Turbo.Primitives.Achievements.Enums.AchievementState.Enabled
                : Turbo.Primitives.Achievements.Enums.AchievementState.Disabled,
        };
    }
}
