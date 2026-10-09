using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Catalog;

/// <summary>A player's redemption of a voucher: one each, which the unique index holds to.</summary>
[Table("voucher_redemptions")]
[Index(nameof(VoucherEntityId), nameof(PlayerEntityId), IsUnique = true)]
public class VoucherRedemptionEntity : TurboEntity
{
    [Column("voucher_id")]
    public int VoucherEntityId { get; set; }

    [Column("player_id")]
    public int PlayerEntityId { get; set; }

    [ForeignKey(nameof(VoucherEntityId))]
    public VoucherEntity? VoucherEntity { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? PlayerEntity { get; set; }
}
