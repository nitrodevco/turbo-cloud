using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// A catalog page the reception counts down to: the expiring page widget shows the page whose
/// <see cref="ExpiresAt"/> comes first among those still to come, with its own texts and teaser
/// picture. It promotes the page; the catalog itself shows the page as it would anyway.
/// </summary>
[Table("catalog_page_expiries")]
[Index(nameof(CatalogPageEntityId), IsUnique = true)]
[Index(nameof(ExpiresAt))]
public class CatalogPageExpiryEntity : TurboEntity
{
    public const int IMAGE_MAX_LENGTH = 255;

    [Column("page_id")]
    public int CatalogPageEntityId { get; set; }

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    /// <summary>What the client keeps as the page's image; it draws the image library's teaser by the page's name.</summary>
    [Column("image")]
    [StringLength(IMAGE_MAX_LENGTH)]
    public required string Image { get; set; }

    [ForeignKey(nameof(CatalogPageEntityId))]
    public CatalogPageEntity? CatalogPageEntity { get; set; }
}
