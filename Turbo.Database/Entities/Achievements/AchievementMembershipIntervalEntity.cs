using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Table("achievement_membership_intervals")]
[Index(nameof(PlayerId))]
public sealed class AchievementMembershipIntervalEntity
{
    [Key]
    public long Id { get; set; }
    public int PlayerId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public bool Purchased { get; set; }
}
