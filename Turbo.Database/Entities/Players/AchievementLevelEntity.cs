using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Catalog;

namespace Turbo.Database.Entities.Players;

[Table("achievement_levels")]
[Index(nameof(AchievementEntityId), nameof(Level), IsUnique = true)]
public class AchievementLevelEntity : TurboEntity
{
    [Column("achievement_id")]
    public required int AchievementEntityId { get; set; }

    [Column("level")]
    public required int Level { get; set; }

    [Column("goal_count")]
    public required int GoalCount { get; set; }

    [Column("score_reward")]
    [DefaultValue(0)]
    public required int ScoreReward { get; set; }

    [Column("currency_type_id")]
    public int? CurrencyTypeEntityId { get; set; }

    [Column("currency_reward")]
    [DefaultValue(0)]
    public required int CurrencyReward { get; set; }

    [Column("badge_code")]
    public string? BadgeCode { get; set; }

    [ForeignKey(nameof(AchievementEntityId))]
    public AchievementEntity? AchievementEntity { get; set; }

    [ForeignKey(nameof(CurrencyTypeEntityId))]
    public CurrencyTypeEntity? CurrencyTypeEntity { get; set; }
}
