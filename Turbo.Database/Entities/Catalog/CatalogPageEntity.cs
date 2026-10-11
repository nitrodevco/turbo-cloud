using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Database.Entities.Catalog;

[Comment("The catalog's page tree, as both the normal and the Builders Club catalog show it.")]
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

    /// <summary>The layout's images, in the order the layout places them; empty for none.</summary>
    [Column("image_data")]
    public List<string> ImageData { get; set; } = [];

    /// <summary>The layout's texts, in the order the layout places them; empty for none.</summary>
    [Column("text_data")]
    public List<string> TextData { get; set; } = [];

    [Column("sort_order")]
    [DefaultValue(0)]
    public required int SortOrder { get; set; }

    /// <summary>
    /// Which catalogs show the page. Both catalogs are cut from this one tree, and offers and
    /// products follow their page into each.
    /// </summary>
    [Column("display")]
    [DefaultValue(CatalogPageDisplay.Regular)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required CatalogPageDisplay Display { get; set; }

    [ForeignKey(nameof(ParentEntityId))]
    public CatalogPageEntity? ParentEntity { get; set; }

    public IList<CatalogPageEntity>? Children { get; set; }

    public IList<CatalogOfferEntity>? Offers { get; set; }
}
