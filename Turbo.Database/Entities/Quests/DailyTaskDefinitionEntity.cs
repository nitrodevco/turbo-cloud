using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
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
}
