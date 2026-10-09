using Turbo.Primitives.Messages.Incoming.RoomSettings;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Revisions.Revision20260909.Parsers.RoomSettings;

/// <summary>
/// The client's save composer: room id, enabled, detection sensitivity, action, ban seconds,
/// guard enabled, guard seconds, guard sensitivity, confirmed.
/// </summary>
internal class SaveRaidProtectionSettingsMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet) =>
        new SaveRaidProtectionSettingsMessage
        {
            RoomId = packet.PopInt(),
            Settings = new RaidProtectionSettingsUpdateSnapshot
            {
                Enabled = packet.PopBoolean(),
                DetectionSensitivity = packet.PopInt(),
                ActionType = packet.PopInt(),
                BanDurationSeconds = packet.PopInt(),
                GuardEnabled = packet.PopBoolean(),
                GuardDurationSeconds = packet.PopInt(),
                GuardSensitivity = packet.PopInt(),
                Confirmed = packet.PopBoolean(),
            },
        };
}
