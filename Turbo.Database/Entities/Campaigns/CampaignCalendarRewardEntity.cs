using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Campaigns;

/// <summary>What one door of a campaign calendar gives: one row per day at most.</summary>
[Table("campaign_calendar_rewards")]
[Index(nameof(CampaignCalendarEntityId), nameof(DayIndex), IsUnique = true)]
public class CampaignCalendarRewardEntity : TurboEntity
{
    public const int CODE_MAX_LENGTH = 100;
    public const int IMAGE_MAX_LENGTH = 255;

    [Column("campaign_id")]
    public required int CampaignCalendarEntityId { get; set; }

    [Column("day_index")]
    public int DayIndex { get; set; }

    [Column("product_code")]
    [StringLength(CODE_MAX_LENGTH)]
    public required string ProductCode { get; set; }

    [Column("custom_image")]
    [StringLength(IMAGE_MAX_LENGTH)]
    public required string CustomImage { get; set; }

    [Column("furniture_class_name")]
    [StringLength(CODE_MAX_LENGTH)]
    public required string FurnitureClassName { get; set; }

    [Column("badge_code")]
    [StringLength(64)]
    public required string BadgeCode { get; set; }

    [Column("credits")]
    public int Credits { get; set; }

    [Column("currency_type_id")]
    public int? CurrencyTypeId { get; set; }

    [Column("currency_amount")]
    public int CurrencyAmount { get; set; }

    [Column("effect_id")]
    public int? EffectId { get; set; }

    [ForeignKey(nameof(CampaignCalendarEntityId))]
    public CampaignCalendarEntity? CampaignCalendarEntity { get; set; }
}
