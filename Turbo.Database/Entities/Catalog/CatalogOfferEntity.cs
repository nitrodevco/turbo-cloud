using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Catalog;

[Table("catalog_offers")]
public class CatalogOfferEntity : TurboEntity
{
    [Column("page_id")]
    public required int CatalogPageEntityId { get; set; }

    [Column("localization_id")]
    public required string LocalizationId { get; set; }

    [Column("cost_credits")]
    [DefaultValue(0)]
    public required int CostCredits { get; set; }

    [Column("cost_currency")]
    [DefaultValue(0)]
    public required int CostCurrency { get; set; }

    [Column("currency_type")]
    public int? CurrencyType { get; set; }

    [Column("can_gift")]
    [DefaultValue(true)]
    public required bool CanGift { get; set; }

    [Column("can_bundle")]
    [DefaultValue(true)]
    public required bool CanBundle { get; set; }

    [Column("club_level")]
    [DefaultValue(0)]
    public required int ClubLevel { get; set; }

    [Column("visible")]
    [DefaultValue(true)]
    public required bool Visible { get; set; }

    [ForeignKey(nameof(CatalogPageEntityId))]
    public required CatalogPageEntity Page { get; set; }

    [ForeignKey(nameof(CurrencyType))]
    public CurrencyTypeEntity? CurrencyTypeEntity { get; set; }

    public IList<CatalogProductEntity>? Products { get; set; }
}
