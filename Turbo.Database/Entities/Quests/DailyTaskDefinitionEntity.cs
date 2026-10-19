using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Quests;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Database.Entities.Quests;

/// <summary>
/// A daily task a player can be given. <see cref="Code"/> names the client's
/// <c>dailytask.&lt;code&gt;.*</c> texts and image; <see cref="QuestType"/> is one of
/// <c>DailyTaskTypes</c> and says what counts, with <see cref="Target"/> its comma-separated furni
/// definition names for a find task. One reward per task, as every official task shows.
/// </summary>
[Table("daily_task_definitions")]
[Index(nameof(Code), IsUnique = true)]
public class DailyTaskDefinitionEntity : TurboEntity
{
    [Column("code")]
    [StringLength(64)]
    public required string Code { get; set; }

    [Column("quest_type")]
    [StringLength(32)]
    public required string QuestType { get; set; }

    [Column("target")]
    [StringLength(512)]
    public string Target { get; set; } = "";

    [Column("required_repeats")]
    [DefaultValue(1)]
    public int RequiredRepeats { get; set; } = 1;

    [Column("image_version")]
    [StringLength(32)]
    public string ImageVersion { get; set; } = "";

    [Column("catalog_name")]
    [StringLength(64)]
    public string CatalogName { get; set; } = "";

    /// <summary>Given once the day's other tasks are all completed.</summary>
    [Column("is_bonus")]
    [DefaultValue(false)]
    public bool IsBonus { get; set; }

    [Column("enabled")]
    [DefaultValue(true)]
    public bool Enabled { get; set; } = true;

    [Column("reward_product_type")]
    [DefaultValue(ProductDisplayType.ActivityPoints)]
    public ProductDisplayType RewardProductType { get; set; } = ProductDisplayType.ActivityPoints;

    /// <summary>What the reward is: the point type for points (0 is duckets), the code for a badge.</summary>
    [Column("reward_type_id")]
    [StringLength(64)]
    public string RewardTypeId { get; set; } = "0";

    [Column("reward_amount")]
    [DefaultValue(0)]
    public int RewardAmount { get; set; }

    /// <summary>
    /// Why the definition can't be given, or null when it can: a kind the server counts, a target
    /// for a find task, an explore count its counted rooms fit in, at least one step, and a reward
    /// it can grant (points of a numbered type, or a badge by its code).
    /// </summary>
    public string? Problem()
    {
        if (RequiredRepeats < 1)
            return "It needs at least one step.";

        switch (QuestType)
        {
            // Each counted room takes its id and a comma.
            case DailyTaskTypes.EXPLORE
                when RequiredRepeats
                    * (int.MaxValue.ToString(CultureInfo.InvariantCulture).Length + 1)
                    > PlayerDailyTaskEntity.COUNTED_MAX_LENGTH:
                return $"An explore task counts at most {PlayerDailyTaskEntity.COUNTED_MAX_LENGTH / (int.MaxValue.ToString(CultureInfo.InvariantCulture).Length + 1)} rooms.";
            case DailyTaskTypes.EXPLORE:
                break;
            case DailyTaskTypes.FIND_FURNI when Targets().Length == 0:
                return "A find task needs the furni to find: their names, with commas between.";
            case DailyTaskTypes.FIND_FURNI:
                break;
            default:
                return $"The server counts no {QuestType} task: it is {DailyTaskTypes.EXPLORE} or {DailyTaskTypes.FIND_FURNI}.";
        }

        if (RewardAmount == 0)
            return null;

        return RewardProductType switch
        {
            ProductDisplayType.ActivityPoints
                when int.TryParse(
                    RewardTypeId,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out _
                ) => null,
            ProductDisplayType.ActivityPoints =>
                "A points reward names its point type by number: 0 is duckets.",
            ProductDisplayType.Badge when RewardTypeId.Length > 0 => null,
            ProductDisplayType.Badge => "A badge reward names its badge code.",
            _ => "A task rewards points or a badge.",
        };
    }

    /// <summary>A find task's furni definition names.</summary>
    public string[] Targets() =>
        Target.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
