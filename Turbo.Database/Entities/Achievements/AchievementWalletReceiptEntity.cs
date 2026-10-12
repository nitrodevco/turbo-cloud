using System;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Achievements;

[Comment(
    "Currency an achievement level paid a player, one row per payment, so a retry never pays twice."
)]
[Table("achievement_wallet_receipts")]
[PrimaryKey(nameof(PlayerId), nameof(AwardKey))]
public sealed class AchievementWalletReceiptEntity
{
    public int PlayerId { get; set; }
    public required string AwardKey { get; set; }
    public required string PayloadJson { get; set; }

    /// <summary>When the row was written, so old receipts can be purged.</summary>
    [Column("created_at")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public DateTime CreatedAt { get; set; }
}
