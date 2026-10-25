using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Players.Permissions;

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

    /// <summary>
    /// A heading with no page of its own, as Habbo's "Hot now": choosing it opens and closes the
    /// pages under it and leaves the page that was open on show.
    /// </summary>
    [Column("folder_only")]
    [DefaultValue(false)]
    public bool FolderOnly { get; set; }

    /// <summary>
    /// The lowest rank that sees the page and what is under it, as retro emulators and their CMS
    /// panels store it: compared against the player's security level, a regular player (level 0)
    /// counting as rank 1, so the default of 1 is everyone's. See <c>docs/permissions.md</c> §16.
    /// </summary>
    [Column("min_rank")]
    [DefaultValue(1)]
    public int MinRank { get; set; } = 1;

    /// <summary>
    /// A permission node the player must also hold to see the page and what is under it (an event
    /// team's page, say), or null for none. What <see cref="MinRank"/> cannot say.
    /// </summary>
    [Column("required_node")]
    [MaxLength(PermissionNodeFormat.MAX_LENGTH)]
    public string? RequiredNode { get; set; }

    [ForeignKey(nameof(ParentEntityId))]
    public CatalogPageEntity? ParentEntity { get; set; }

    public IList<CatalogPageEntity>? Children { get; set; }

    public IList<CatalogOfferEntity>? Offers { get; set; }
}
