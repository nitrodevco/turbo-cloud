using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// Progress supplied by the credit-purchase integration for a particular campaign. A missing row
/// means no qualifying purchases. Wallet credits and catalog spending do not modify this row.
/// </summary>
[Table("player_bonus_rare_progress")]
[Index(nameof(PlayerEntityId), nameof(CampaignId), IsUnique = true)]
public class PlayerBonusRareProgressEntity : TurboEntity
{
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    [Column("campaign_id")]
    [MaxLength(100)]
    public required string CampaignId { get; set; }

    /// <summary>Unredeemed qualifying credits; reset by the purchase integration after delivery.</summary>
    [Column("credits_toward_next_reward")]
    [DefaultValue(0)]
    public int CreditsTowardNextReward { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
