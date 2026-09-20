using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Players.Enums;

namespace Turbo.Database.Entities.Catalog;

[Table("catalog_products")]
public class CatalogProductEntity : TurboEntity
{
    [Column("offer_id")]
    public required int CatalogOfferEntityId { get; set; }

    [Column("product_type")]
    [DefaultValue(ProductType.Floor)]
    public required ProductType ProductType { get; set; }

    [Column("definition_id")]
    public int? FurnitureDefinitionEntityId { get; set; }

    [Column("extra_param")]
    public string? ExtraParam { get; set; }

    [Column("quantity")]
    [DefaultValue(1)]
    public required int Quantity { get; set; }

    /// <summary>
    /// The subscription this product grants, for a product that sells membership rather than
    /// furniture. Null for everything else. <see cref="ProductType.HabboClub"/> is the letter the
    /// client is sent and says nothing about which club, so the two are not the same column.
    /// </summary>
    [Column("subscription_type")]
    public SubscriptionType? SubscriptionType { get; set; }

    /// <summary>Days of membership the product grants. Zero unless it grants a subscription.</summary>
    [Column("subscription_days")]
    [DefaultValue(0)]
    public int SubscriptionDays { get; set; }

    [ForeignKey(nameof(CatalogOfferEntityId))]
    public required CatalogOfferEntity Offer { get; set; }

    [ForeignKey(nameof(FurnitureDefinitionEntityId))]
    public FurnitureDefinitionEntity? FurnitureDefinition { get; set; }
}
