using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Roomsettings;

/// <summary>The room id, then the result code, then the rest of the settings.</summary>
internal class RaidProtectionSettingsResultMessageComposerSerializer(int header)
    : AbstractSerializer<RaidProtectionSettingsResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RaidProtectionSettingsResultMessageComposer message
    )
    {
        packet
            .WriteInteger(message.Result.Settings.RoomId)
            .WriteInteger((int)message.Result.Result);
        RaidProtectionSettingsSerializer.SerializeAfterRoomId(packet, message.Result.Settings);
    }
}
