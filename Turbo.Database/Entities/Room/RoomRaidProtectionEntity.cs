using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Database.Entities.Room;

/// <summary>
/// A room's raid protection settings. A room without a row has the defaults below, which are
/// what a new row starts from; the row goes with its room. They are what Habbo's window shows
/// for a room never set up (official client, 2026-10-09, evidence raid.png): off, medium
/// sensitivity, temporary ban for 15 minutes, door guard off for 15 minutes at medium.
/// </summary>
[Table("room_raid_protection")]
[Index(nameof(RoomEntityId), IsUnique = true)]
public class RoomRaidProtectionEntity : TurboEntity
{
    [Column("room_id")]
    public required int RoomEntityId { get; set; }

    [Column("enabled")]
    [DefaultValue(false)]
    public bool Enabled { get; set; }

    [Column("detection_sensitivity")]
    [DefaultValue(RaidSensitivityType.Medium)]
    public RaidSensitivityType DetectionSensitivity { get; set; } = RaidSensitivityType.Medium;

    [Column("action_type")]
    [DefaultValue(RaidActionType.TemporaryBan)]
    public RaidActionType ActionType { get; set; } = RaidActionType.TemporaryBan;

    [Column("ban_duration_seconds")]
    [DefaultValue(900)]
    public int BanDurationSeconds { get; set; } = 900;

    [Column("guard_enabled")]
    [DefaultValue(false)]
    public bool GuardEnabled { get; set; }

    [Column("guard_duration_seconds")]
    [DefaultValue(900)]
    public int GuardDurationSeconds { get; set; } = 900;

    [Column("guard_sensitivity")]
    [DefaultValue(RaidSensitivityType.Medium)]
    public RaidSensitivityType GuardSensitivity { get; set; } = RaidSensitivityType.Medium;

    /// <summary>When the last raid was detected; null for never.</summary>
    [Column("last_raid_at")]
    public DateTime? LastRaidAt { get; set; }

    [ForeignKey(nameof(RoomEntityId))]
    public RoomEntity? RoomEntity { get; set; }
}
