using Turbo.Primitives.Messages.Outgoing.Roomsettings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Roomsettings;

internal class RoomSettingsErrorEventMessageComposerSerializer(int header)
    : AbstractSerializer<RoomSettingsErrorEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        RoomSettingsErrorEventMessageComposer message
    )
    {
        packet.WriteInteger(message.RoomId).WriteInteger(message.ErrorCode);
    }
}
