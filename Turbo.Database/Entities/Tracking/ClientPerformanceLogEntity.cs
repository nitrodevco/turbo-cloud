using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Turbo.Database.Entities.Tracking;

/// <summary>
/// A client's performance report (<c>PerformanceLog</c>): its frame time, memory and slow frames,
/// with the browser, system and kind of device read from its user agent. No address is kept.
/// Raw rows expire after <c>Turbo:ClientTelemetry:RawRetentionDays</c>; their daily figures stay in
/// <see cref="ClientTelemetryDailyEntity"/>.
/// </summary>
[Table("client_performance_logs")]
[Index(nameof(CreatedAt))]
[Index(nameof(PlayerId), nameof(CreatedAt))]
[Index(nameof(RoomId), nameof(CreatedAt))]
public class ClientPerformanceLogEntity
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("created_at")]
    public required DateTime CreatedAt { get; set; }

    [Column("player_id")]
    public required int PlayerId { get; set; }

    /// <summary>The room the player was in, if any.</summary>
    [Column("room_id")]
    public int? RoomId { get; set; }

    [Column("elapsed_seconds")]
    public required int ElapsedSeconds { get; set; }

    [Column("user_agent")]
    [MaxLength(512)]
    public required string UserAgent { get; set; }

    [Column("flash_version")]
    [MaxLength(64)]
    public required string FlashVersion { get; set; }

    [Column("os")]
    [MaxLength(64)]
    public required string Os { get; set; }

    [Column("browser")]
    [MaxLength(64)]
    public required string Browser { get; set; }

    [Column("browser_family")]
    [MaxLength(32)]
    public required string BrowserFamily { get; set; }

    [Column("os_family")]
    [MaxLength(32)]
    public required string OsFamily { get; set; }

    [Column("device_type")]
    [MaxLength(16)]
    public required string DeviceType { get; set; }

    [Column("is_debugger")]
    public required bool IsDebugger { get; set; }

    /// <summary>The memory the client holds, in KB; -1 when its browser does not say.</summary>
    [Column("memory_kb")]
    public required int MemoryKb { get; set; }

    [Column("garbage_collections")]
    public required int GarbageCollections { get; set; }

    /// <summary>The average time between frames, in milliseconds; 0 when there were none.</summary>
    [Column("average_update_interval_ms")]
    public required int AverageUpdateIntervalMs { get; set; }

    [Column("slow_updates")]
    public required int SlowUpdates { get; set; }
}
