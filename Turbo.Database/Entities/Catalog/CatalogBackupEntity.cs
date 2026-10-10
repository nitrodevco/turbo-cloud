using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// A copy of the catalog's pages, offers, products and featured items as they were saved when it
/// was taken, which the editor can roll the catalog back to. <see cref="Data"/> holds the rows,
/// compressed; the counts are kept beside it so a list of backups need not read it.
/// </summary>
[Table("catalog_backups")]
public class CatalogBackupEntity : TurboEntity
{
    public const int NAME_MAX_LENGTH = 100;

    [Column("name")]
    [MaxLength(NAME_MAX_LENGTH)]
    public required string Name { get; set; }

    /// <summary>Who took it; not a link to the player, so a backup outlives its maker.</summary>
    [Column("player_id")]
    public int PlayerId { get; set; }

    /// <summary>Taken by the editor itself, before a rollback replaced what was there.</summary>
    [Column("automatic")]
    public bool Automatic { get; set; }

    [Column("pages")]
    public int Pages { get; set; }

    [Column("offers")]
    public int Offers { get; set; }

    [Column("products")]
    public int Products { get; set; }

    [Column("featured_items")]
    public int FeaturedItems { get; set; }

    [Column("data")]
    public required byte[] Data { get; set; }
}
