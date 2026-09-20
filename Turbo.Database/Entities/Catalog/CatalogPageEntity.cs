using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Database.Entities.Catalog;

[Table("catalog_pages")]
// Each catalog is loaded on its own, so the tree is read by its type before anything else.
[Index(nameof(CatalogType))]
public class CatalogPageEntity : TurboEntity
{
    /// <summary>
    /// Which catalog this page belongs to. Offers and products inherit it from their page, so
    /// this is the only row that says which tree anything is in.
    /// </summary>
    [Column("catalog_type")]
    [DefaultValue(CatalogType.Normal)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required CatalogType CatalogType { get; set; }

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

    [Column("sort_order")]
    [DefaultValue(0)]
    public required int SortOrder { get; set; }

    [Column("visible")]
    [DefaultValue(true)]
    public required bool Visible { get; set; }

    [ForeignKey(nameof(ParentEntityId))]
    public CatalogPageEntity? ParentEntity { get; set; }

    public IList<CatalogPageEntity>? Children { get; set; }

    public IList<CatalogOfferEntity>? Offers { get; set; }
}
