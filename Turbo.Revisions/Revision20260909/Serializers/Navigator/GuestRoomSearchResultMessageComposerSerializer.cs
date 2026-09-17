using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Navigator.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Navigator;

internal class GuestRoomSearchResultMessageComposerSerializer(int header)
    : AbstractSerializer<GuestRoomSearchResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        GuestRoomSearchResultMessageComposer message
    )
    {
        packet
            .WriteInteger((int)message.SearchType)
            .WriteString(message.SearchParam)
            .WriteInteger(message.Rooms.Length);

        foreach (var room in message.Rooms)
            RoomSettingsSerializer.Serialize(packet, room);

        packet.WriteBoolean(false); // no ad room
    }
}
