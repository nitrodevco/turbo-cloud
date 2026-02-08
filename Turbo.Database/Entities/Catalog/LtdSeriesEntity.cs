using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// Configuration for an LTD (Limited Edition) series.
/// </summary>
[Table("ltd_series")]
public class LtdSeriesEntity : TurboEntity
{
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
    /// How long the raffle queue is open (in seconds) before drawing.
    /// </summary>
    [Column("raffle_window_seconds")]
    [DefaultValue(30)]
    public required int RaffleWindowSeconds { get; set; } = 30;

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
