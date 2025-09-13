using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Catalog;

[Table("catalog_pages")]
public class CatalogPageEntity : TurboEntity
{
    [Column("parent_id")]
    public int? ParentEntityId { get; set; }

    [Column("localization")]
    [MaxLength(50)]
    public required string Localization { get; set; }

    [Column("name")]
    [MaxLength(50)]
    public string? Name { get; set; }

    [Column("icon")]
    [DefaultValue(0)]
    public required int Icon { get; set; }

    [Column("layout")]
    [DefaultValue("default_3x3")]
    [MaxLength(50)]
    public required string Layout { get; set; }

    [Column("image_data")]
    public List<string>? ImageData { get; set; } = null!;

    [Column("text_data")]
    public List<string>? TextData { get; set; } = null!;

    [Column("visible")]
    [DefaultValue(true)]
    public required bool Visible { get; set; }

    [ForeignKey(nameof(ParentEntityId))]
    public CatalogPageEntity? ParentEntity { get; set; }

    public IList<CatalogPageEntity>? Children { get; set; }

    public IList<CatalogOfferEntity>? Offers { get; set; }
}
