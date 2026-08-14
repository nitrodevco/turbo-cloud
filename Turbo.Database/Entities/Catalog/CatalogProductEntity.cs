using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Database.Entities.Furniture;
using Turbo.Primitives.Furniture.Enums;

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

    [ForeignKey(nameof(CatalogOfferEntityId))]
    public required CatalogOfferEntity Offer { get; set; }

    [ForeignKey(nameof(FurnitureDefinitionEntityId))]
    public FurnitureDefinitionEntity? FurnitureDefinition { get; set; }
}
