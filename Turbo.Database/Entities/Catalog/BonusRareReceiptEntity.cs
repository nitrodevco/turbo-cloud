using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// Credits bought, recorded once for the bonus rare under the purchase integration's own
/// reference: a reference recorded again counts nothing.
/// </summary>
[Table("bonus_rare_receipts")]
[Index(nameof(Reference), IsUnique = true)]
public class BonusRareReceiptEntity : TurboEntity
{
    public const int REFERENCE_MAX_LENGTH = 100;

    [Column("reference")]
    [StringLength(REFERENCE_MAX_LENGTH)]
    public required string Reference { get; set; }

    [Column("player_id")]
    public int PlayerEntityId { get; set; }

    [Column("campaign_code")]
    [StringLength(BonusRareCampaignEntity.CODE_MAX_LENGTH)]
    public required string CampaignCode { get; set; }

    [Column("credits")]
    public int Credits { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
