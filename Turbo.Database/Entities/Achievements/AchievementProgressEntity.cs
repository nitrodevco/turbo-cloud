using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Table("achievement_progress")]
[PrimaryKey(nameof(PlayerId), nameof(AchievementId))]
public sealed class AchievementProgressEntity
{
    public int PlayerId { get; set; }
    public int AchievementId { get; set; }
    public long Value { get; set; }
    public long ForwardAdjustment { get; set; }
    public long Streak { get; set; }
    public DateTime? LastDayUtc { get; set; }
    public int EarnedLevel { get; set; }

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public string DistinctValuesJson { get; set; } = "[]";

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public string IntervalsJson { get; set; } = "[]";
}
