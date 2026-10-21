using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Tracking;

/// <summary>
/// One day's figure of one kind (<c>ClientTelemetryKinds</c>) for one set of keys (a browser, a
/// room, an event...), rolled up from the raw client telemetry so it outlives it: kept for
/// <c>Turbo:ClientTelemetry:RollupRetentionDays</c>. Unused keys are empty.
/// </summary>
[Table("client_telemetry_daily")]
[Index(nameof(Day), nameof(Kind), nameof(Key1), nameof(Key2), nameof(Key3), IsUnique = true)]
public class ClientTelemetryDailyEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("day")]
    public required DateOnly Day { get; set; }

    [Column("kind")]
    [MaxLength(32)]
    public required string Kind { get; set; }

    [Column("key1")]
    [MaxLength(64)]
    public required string Key1 { get; set; }

    [Column("key2")]
    [MaxLength(128)]
    public required string Key2 { get; set; }

    [Column("key3")]
    [MaxLength(128)]
    public required string Key3 { get; set; }

    /// <summary>The reports, events or logins counted.</summary>
    [Column("samples")]
    public required int Samples { get; set; }

    /// <summary>The different players among them.</summary>
    [Column("players")]
    public required int Players { get; set; }

    [Column("sum")]
    public required long Sum { get; set; }

    [Column("p50")]
    public required int P50 { get; set; }

    [Column("p95")]
    public required int P95 { get; set; }

    [Column("max")]
    public required int Max { get; set; }
}
