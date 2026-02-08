using Turbo.Primitives.Messages.Outgoing.NewNavigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.NewNavigator;

internal class NavigatorLiftedRoomsMessageSerializer(int header)
    : AbstractSerializer<NavigatorLiftedRoomsMessage>(header)
{
    protected override void Serialize(IServerPacket packet, NavigatorLiftedRoomsMessage message)
    {
        packet.WriteInteger(message.LiftedRooms.Count);

        foreach (var room in message.LiftedRooms)
        {
            packet.WriteInteger(room.FlatId);
            packet.WriteInteger(room.AreaId);
            packet.WriteString(room.Image);
            packet.WriteString(room.Caption);
        }
    }
}
