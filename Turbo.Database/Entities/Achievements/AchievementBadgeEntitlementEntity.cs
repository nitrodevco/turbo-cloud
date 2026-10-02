using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Table("achievement_badge_entitlements")]
[PrimaryKey(nameof(PlayerId), nameof(AchievementId))]
public sealed class AchievementBadgeEntitlementEntity
{
    public int PlayerId { get; set; }
    public int AchievementId { get; set; }
    public required string BadgeCode { get; set; }
    public int Level { get; set; }
}
