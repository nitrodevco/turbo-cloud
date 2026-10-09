using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Furniture;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// A voucher code and what it gives. <see cref="Uses"/> is counted up in the database as each
/// redemption is made, never past <see cref="MaxUses"/>; who redeemed it is in
/// <c>voucher_redemptions</c>.
/// </summary>
[Table("vouchers")]
[Index(nameof(Code), IsUnique = true)]
public class VoucherEntity : TurboEntity
{
    public const int CODE_MAX_LENGTH = 64;
    public const int BADGE_MAX_LENGTH = 64;
    public const int NOTE_MAX_LENGTH = 255;

    [Column("code")]
    [StringLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [Column("credits")]
    public int Credits { get; set; }

    [Column("currency_type_id")]
    public int? CurrencyTypeEntityId { get; set; }

    [Column("currency_amount")]
    public int CurrencyAmount { get; set; }

    [Column("furniture_definition_id")]
    public int? FurnitureDefinitionEntityId { get; set; }

    [Column("furniture_quantity")]
    public int FurnitureQuantity { get; set; }

    [Column("badge_code")]
    [StringLength(BADGE_MAX_LENGTH)]
    public string? BadgeCode { get; set; }

    [Column("max_uses")]
    public int? MaxUses { get; set; }

    [Column("uses")]
    public int Uses { get; set; }

    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }

    [Column("enabled")]
    public bool Enabled { get; set; } = true;

    [Column("note")]
    [StringLength(NOTE_MAX_LENGTH)]
    public required string Note { get; set; }

    [ForeignKey(nameof(CurrencyTypeEntityId))]
    public CurrencyTypeEntity? CurrencyTypeEntity { get; set; }

    [ForeignKey(nameof(FurnitureDefinitionEntityId))]
    public FurnitureDefinitionEntity? FurnitureDefinitionEntity { get; set; }
}
