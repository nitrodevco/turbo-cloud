using Turbo.Database.Entities.Quests;
using Turbo.Primitives.Quests.Snapshots;

namespace Turbo.Database.Extensions;

/// <summary>Daily tasks.</summary>
public static class QuestEntityExtensions
{
    public static DailyTaskRewardSnapshot ToRewardSnapshot(
        this DailyTaskDefinitionEntity definition
    ) =>
        new()
        {
            ProductType = definition.RewardProductType,
            RewardTypeId = definition.RewardTypeId,
            ExtraParams = "",
            Amount = definition.RewardAmount,
        };

    /// <summary>
    /// The reward is listed at its own amount; a club member's doubled duckets are credited on
    /// claim, since no capture shows whether Habbo lists them doubled.
    /// </summary>
    /// <param name="secondsLeft">Seconds until the task's day ends, negative for an earlier day's.</param>
    public static DailyTaskSnapshot ToSnapshot(
        this PlayerDailyTaskEntity task,
        DailyTaskDefinitionEntity definition,
        int secondsLeft
    ) =>
        new()
        {
            TaskId = task.Id,
            TaskCode = definition.Code,
            QuestTypeCode = definition.QuestType,
            IsBonus = definition.IsBonus,
            ImageVersion = definition.ImageVersion,
            CatalogName = definition.CatalogName,
            RequiredRepeats = definition.RequiredRepeats,
            Repeats = task.Repeats,
            Status = task.Status,
            SecondsLeft = secondsLeft,
            Rewards = definition.RewardAmount > 0 ? [definition.ToRewardSnapshot()] : [],
        };
}
