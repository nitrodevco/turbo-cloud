using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Catalog;

[Table("currency_type")]
public class CurrencyTypeEntity : TurboEntity
{
    [Column("currency_key")]
    [MaxLength(255)]
    public required string CurrencyKey { get; set; }

    [Column("is_activity_points")]
    [DefaultValue(false)]
    public required bool IsActivityPoints { get; set; }

    [Column("activity_point_type")]
    public int? ActivityPointType { get; set; }

    [Column("name")]
    [MaxLength(255)]
    public string? Name { get; set; }

    [Column("enabled")]
    [DefaultValue(true)]
    public required bool Enabled { get; set; }

    public IList<CatalogOfferEntity>? CatalogOffers { get; set; }

    public IList<PlayerCurrencyEntity>? PlayerCurrencies { get; set; }
}
