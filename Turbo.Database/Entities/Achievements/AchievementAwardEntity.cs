using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Table("achievement_awards")]
[PrimaryKey(nameof(PlayerId), nameof(AchievementId), nameof(Level))]
[Index(nameof(Completed), nameof(PlayerId))]
public sealed class AchievementAwardEntity
{
    public int PlayerId { get; set; }
    public int AchievementId { get; set; }
    public int Level { get; set; }
    public int Revision { get; set; }

    [MaxLength(160)]
    public required string AwardKey { get; set; }

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public required string DefinitionJson { get; set; }

    [Column(TypeName = "longtext")]
    [MaxLength(int.MaxValue)]
    public required string RewardJson { get; set; }
    public DateTime EarnedAtUtc { get; set; }
    public bool Completed { get; set; }
    public bool Presented { get; set; }
    public int DeliveredRewards { get; set; }
    public string? BlockedReason { get; set; }
}
