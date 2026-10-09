using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Revisions.Revision20260909.Serializers.Roomsettings;

/// <summary>
/// The client's <c>RaidProtectionSettingsSnapshot.readAfterRoomId</c>: everything after the room
/// id, which the result message separates from the rest with its code.
/// </summary>
internal static class RaidProtectionSettingsSerializer
{
    public static void SerializeAfterRoomId(
        IServerPacket packet,
        RaidProtectionSettingsSnapshot settings
    ) =>
        packet
            .WriteBoolean(settings.Enabled)
            .WriteInteger((int)settings.DetectionSensitivity)
            .WriteInteger((int)settings.ActionType)
            .WriteInteger(settings.BanDurationSeconds)
            .WriteBoolean(settings.GuardEnabled)
            .WriteInteger(settings.GuardDurationSeconds)
            .WriteInteger((int)settings.GuardSensitivity)
            .WriteBoolean(settings.IncidentActive)
            .WriteInteger(settings.LastRaidAtEpochSeconds);
}
