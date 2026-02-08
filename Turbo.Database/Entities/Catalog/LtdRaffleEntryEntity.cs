using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Turbo.Database.Entities.Players;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// An entry in an LTD raffle queue.
/// </summary>
[Table("ltd_raffle_entries")]
public class LtdRaffleEntryEntity : TurboEntity
{
    /// <summary>
    /// The LTD series this entry is for.
    /// </summary>
    [Column("series_id")]
    public required int SeriesEntityId { get; set; }

    /// <summary>
    /// The player who entered the raffle.
    /// </summary>
    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>
    /// UUID for the raffle batch this entry belongs to.
    /// </summary>
    [Column("batch_id")]
    [MaxLength(36)]
    public required string BatchId { get; set; }

    /// <summary>
    /// When the player entered the raffle.
    /// </summary>
    [Column("entered_at")]
    public required DateTime EnteredAt { get; set; }

    /// <summary>
    /// The result of the raffle: 'pending', 'won', 'lost'.
    /// </summary>
    [Column("result")]
    [MaxLength(20)]
    [DefaultValue("pending")]
    public required string Result { get; set; } = "pending";

    /// <summary>
    /// The serial number assigned (if won).
    /// </summary>
    [Column("serial_number")]
    public int? SerialNumber { get; set; }

    /// <summary>
    /// When the raffle was processed.
    /// </summary>
    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [ForeignKey(nameof(SeriesEntityId))]
    public LtdSeriesEntity? Series { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? Player { get; set; }
}
