using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Players.Enums.Wallet;

namespace Turbo.Database.Entities.Catalog;

[Comment(
    "The currencies a hotel has (credits, duckets, diamonds, ...) and how the client knows them."
)]
[Table("catalog_currency_types")]
public class CurrencyTypeEntity : TurboEntity
{
    [Column("name")]
    public required string Name { get; set; }

    [Column("type")]
    public required CurrencyType CurrencyType { get; set; }

    [Column("activity_point_type")]
    public int? ActivityPointType { get; set; }

    [Column("enabled")]
    [DefaultValue(true)]
    public required bool Enabled { get; set; }

    public List<CatalogOfferEntity>? CatalogOffers { get; set; }

    public List<PlayerCurrencyEntity>? PlayerCurrencies { get; set; }
}
