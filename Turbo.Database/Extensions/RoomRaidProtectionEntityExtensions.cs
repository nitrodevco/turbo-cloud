using System;
using Turbo.Database.Entities.Room;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Database.Extensions;

public static class RoomRaidProtectionEntityExtensions
{
    /// <summary>
    /// The row as the client reads it. Whether a raid is happening now is the room's to say, so
    /// it is a parameter.
    /// </summary>
    public static RaidProtectionSettingsSnapshot ToSnapshot(
        this RoomRaidProtectionEntity entity,
        RoomId roomId,
        bool incidentActive
    ) =>
        new()
        {
            RoomId = roomId,
            Enabled = entity.Enabled,
            DetectionSensitivity = entity.DetectionSensitivity,
            ActionType = entity.ActionType,
            BanDurationSeconds = entity.BanDurationSeconds,
            GuardEnabled = entity.GuardEnabled,
            GuardDurationSeconds = entity.GuardDurationSeconds,
            GuardSensitivity = entity.GuardSensitivity,
            IncidentActive = incidentActive,
            LastRaidAtEpochSeconds = entity.LastRaidAt is { } at
                ? (int)
                    new DateTimeOffset(
                        DateTime.SpecifyKind(at, DateTimeKind.Utc)
                    ).ToUnixTimeSeconds()
                : 0,
        };
}
