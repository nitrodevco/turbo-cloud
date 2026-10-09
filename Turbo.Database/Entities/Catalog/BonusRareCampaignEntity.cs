using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// A bonus rare campaign: a furniture given for every <see cref="CreditsRequired"/> credits a
/// player brings in, counted as <see cref="Source"/> says. The last one started and not ended
/// runs. Progress is kept by <see cref="Code"/> in <c>player_bonus_rare_progress</c>.
/// </summary>
[Table("bonus_rare_campaigns")]
[Index(nameof(Code), IsUnique = true)]
[Index(nameof(StartsAt))]
public class BonusRareCampaignEntity : TurboEntity
{
    public const int CODE_MAX_LENGTH = 100;
    public const int NAME_MAX_LENGTH = 255;

    [Column("code")]
    [StringLength(CODE_MAX_LENGTH)]
    public required string Code { get; set; }

    [Column("furniture_name")]
    [StringLength(NAME_MAX_LENGTH)]
    public required string FurnitureName { get; set; }

    [Column("product_code")]
    [StringLength(NAME_MAX_LENGTH)]
    public required string ProductCode { get; set; }

    [Column("credits_required")]
    public int CreditsRequired { get; set; }

    [Column("source")]
    public BonusRareSource Source { get; set; }

    [Column("starts_at")]
    public DateTime StartsAt { get; set; }

    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }
}
