using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// Configuration for an LTD (Limited Edition) series.
/// </summary>
[Comment("Configuration for an LTD (Limited Edition) series.")]
[Table(TABLE)]
public class LtdSeriesEntity : TurboEntity
{
    /// <summary>The table's name.</summary>
    public const string TABLE = "catalog_ltd_series";

    /// <summary>
    /// Links to the catalog product this LTD series is for.
    /// </summary>
    [Column("catalog_product_id")]
    public required int CatalogProductEntityId { get; set; }

    /// <summary>
    /// Total items in this series (e.g., 500).
    /// </summary>
    [Column("total_quantity")]
    public required int TotalQuantity { get; set; }

    /// <summary>
    /// How many items are left to mint.
    /// </summary>
    [Column("remaining_quantity")]
    public required int RemainingQuantity { get; set; }

    /// <summary>
    /// How long the opening raffle gathers buyers before it draws, in seconds; 0 sells first
    /// come, first served. Habbo's is 20.
    /// </summary>
    [Column("raffle_window_seconds")]
    [DefaultValue(20)]
    public required int RaffleWindowSeconds { get; set; } = 20;

    /// <summary>
    /// Whether this series is currently active.
    /// </summary>
    [Column("is_active")]
    [DefaultValue(true)]
    public required bool IsActive { get; set; } = true;

    /// <summary>
    /// Whether the initial 20s raffle draw has already occurred.
    /// </summary>
    [Column("has_raffle_finished")]
    [DefaultValue(false)]
    public bool IsRaffleFinished { get; set; } = false;

    /// <summary>
    /// When this LTD becomes available (null = immediately).
    /// </summary>
    [Column("starts_at")]
    public DateTime? StartsAt { get; set; }

    /// <summary>
    /// When this LTD sale ends (null = never).
    /// </summary>
    [Column("ends_at")]
    public DateTime? EndsAt { get; set; }

    [ForeignKey(nameof(CatalogProductEntityId))]
    public CatalogProductEntity? CatalogProduct { get; set; }
}
