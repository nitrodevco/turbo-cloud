using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Table("achievement_wallet_receipts")]
[PrimaryKey(nameof(PlayerId), nameof(AwardKey))]
public sealed class AchievementWalletReceiptEntity
{
    public int PlayerId { get; set; }
    public required string AwardKey { get; set; }
    public required string PayloadJson { get; set; }
}
