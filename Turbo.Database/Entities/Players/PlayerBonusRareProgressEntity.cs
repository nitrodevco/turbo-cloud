using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Players;

/// <summary>
/// A player's progress toward a bonus rare campaign's reward, by the campaign's code. Only what
/// the campaign counts adds to it: credits bought, recorded under a receipt, or credits spent in
/// the catalogue. A missing row means nothing counted yet.
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

    /// <summary>Counted credits not yet turned into a reward; the target is taken off as each is given.</summary>
    [Column("credits_toward_next_reward")]
    [DefaultValue(0)]
    public int CreditsTowardNextReward { get; set; }

    /// <summary>Rewards given to the player in this campaign.</summary>
    [Column("rewards_received")]
    [DefaultValue(0)]
    public int RewardsReceived { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
