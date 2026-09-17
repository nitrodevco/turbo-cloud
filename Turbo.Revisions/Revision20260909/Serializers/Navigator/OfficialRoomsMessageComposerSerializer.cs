using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Navigator.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Navigator;

internal class OfficialRoomsMessageComposerSerializer(int header)
    : AbstractSerializer<OfficialRoomsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, OfficialRoomsMessageComposer message)
    {
        packet.WriteInteger(message.Rooms.Length);

        for (var index = 0; index < message.Rooms.Length; index++)
        {
            var room = message.Rooms[index];

            packet
                .WriteInteger(index)
                .WriteString(room.Name) // popup caption
                .WriteString(room.Description) // popup description
                .WriteInteger(1) // show details
                .WriteString(room.Name) // picture text
                .WriteString(string.Empty) // picture ref
                .WriteInteger(0) // folder id
                .WriteInteger(room.Population)
                .WriteInteger((int)NavigatorOfficialRoomEntryType.GuestRoom);

            RoomSettingsSerializer.Serialize(packet, room);
        }

        packet
            .WriteInteger(0) // no ad room
            .WriteInteger(0); // no promoted room groups
    }
}
