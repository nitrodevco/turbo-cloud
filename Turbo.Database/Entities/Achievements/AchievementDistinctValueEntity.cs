using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

/// <summary>One distinct value a player has contributed to a distinct-count achievement.</summary>
[Table("achievement_distinct_values")]
[PrimaryKey(nameof(PlayerId), nameof(AchievementId), nameof(Value))]
public sealed class AchievementDistinctValueEntity
{
    public int PlayerId { get; set; }
    public int AchievementId { get; set; }

    [MaxLength(512)]
    public required string Value { get; set; }
}
