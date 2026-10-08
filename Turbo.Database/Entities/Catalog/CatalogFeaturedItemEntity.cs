using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// A featured item of the catalog's front page, which the front page layouts draw: the first
/// large on the left, the next three in a list. It goes live with the rest of the catalog when
/// it is published.
/// </summary>
[Table("catalog_featured_items")]
public class CatalogFeaturedItemEntity : TurboEntity
{
    /// <summary>Its place on the front page, from 1.</summary>
    [Column("position")]
    public required int Position { get; set; }

    [Column("title")]
    [MaxLength(100)]
    public required string Title { get; set; }

    /// <summary>The promo picture, a path the client puts after <c>image.library.url</c>.</summary>
    [Column("image")]
    [MaxLength(255)]
    public required string Image { get; set; }

    /// <summary>What <see cref="Value"/> names: a page, an offer or a product code.</summary>
    [Column("type")]
    public required CatalogFrontPageItemType Type { get; set; }

    [Column("value")]
    [MaxLength(100)]
    public required string Value { get; set; }

    /// <summary>When it comes off the front page, in UTC; null for never.</summary>
    [Column("expires_at")]
    public DateTime? ExpiresAt { get; set; }
}
