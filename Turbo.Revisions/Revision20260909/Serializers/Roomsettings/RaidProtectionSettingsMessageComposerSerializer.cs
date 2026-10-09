using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Roomsettings;

internal class RaidProtectionSettingsMessageComposerSerializer(int header)
    : AbstractSerializer<RaidProtectionSettingsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RaidProtectionSettingsMessageComposer message
    )
    {
        packet.WriteInteger(message.Settings.RoomId);
        RaidProtectionSettingsSerializer.SerializeAfterRoomId(packet, message.Settings);
    }
}
