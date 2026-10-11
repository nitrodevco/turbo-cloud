using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Entities.Players;
using Turbo.Primitives.Catalog.Enums;

namespace Turbo.Database.Entities.Catalog;

/// <summary>
/// Every claim on an LTD series: an entry in a raffle's draw, or a sale with no draw. A won claim
/// holds its serial, and no serial of a series is held twice, so this is the record of who was
/// sold which serial.
/// </summary>
[Comment("Every claim on an LTD series: raffle entries and sales, with the serial each won.")]
[Table("catalog_ltd_entries")]
// "Has this player already won this series" and "this draw's entry for this player".
[Index(nameof(SeriesEntityId), nameof(PlayerEntityId), nameof(Result))]
[Index(nameof(BatchId), nameof(PlayerEntityId))]
public class LtdRaffleEntryEntity : TurboEntity
{
    [Column("series_id")]
    public required int SeriesEntityId { get; set; }

    [Column("player_id")]
    public required int PlayerEntityId { get; set; }

    /// <summary>The draw this entry is in; null for a sale made with no draw.</summary>
    [Column("batch_id")]
    public Guid? BatchId { get; set; }

    [Column("result")]
    [DefaultValue(LtdEntryResult.Pending)]
    [DatabaseGenerated(DatabaseGeneratedOption.None)]
    public required LtdEntryResult Result { get; set; }

    /// <summary>The serial won; null until it is won.</summary>
    [Column("serial_number")]
    public int? SerialNumber { get; set; }

    /// <summary>When it was drawn or sold; null while pending.</summary>
    [Column("processed_at")]
    public DateTime? ProcessedAt { get; set; }

    [ForeignKey(nameof(SeriesEntityId))]
    public LtdSeriesEntity? Series { get; set; }

    [ForeignKey(nameof(PlayerEntityId))]
    public PlayerEntity? Player { get; set; }
}
