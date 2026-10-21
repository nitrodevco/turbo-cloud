using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Tracking;

/// <summary>
/// A client's latency test result (<c>LatencyPingReport</c>), with the browser, system and kind
/// of device from the session's last performance report (<c>unknown</c> before its first).
/// </summary>
[Table("client_latency_reports")]
[Index(nameof(CreatedAt))]
[Index(nameof(PlayerId), nameof(CreatedAt))]
[Index(nameof(RoomId), nameof(CreatedAt))]
public class ClientLatencyReportEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("created_at")]
    public required DateTime CreatedAt { get; set; }

    [Column("player_id")]
    public required int PlayerId { get; set; }

    [Column("room_id")]
    public int? RoomId { get; set; }

    [Column("average_ms")]
    public required int AverageMs { get; set; }

    /// <summary>The average of the pings under twice the average, in milliseconds.</summary>
    [Column("valid_average_ms")]
    public required int ValidAverageMs { get; set; }

    [Column("ping_count")]
    public required int PingCount { get; set; }

    [Column("browser_family")]
    [MaxLength(32)]
    public required string BrowserFamily { get; set; }

    [Column("os_family")]
    [MaxLength(32)]
    public required string OsFamily { get; set; }

    [Column("device_type")]
    [MaxLength(16)]
    public required string DeviceType { get; set; }
}
