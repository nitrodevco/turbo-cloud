using Turbo.Primitives.Messages.Outgoing.Userdefinedroomevents;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Userdefinedroomevents;

internal class WiredLinkConnectionsMessageComposerSerializer(int header)
    : AbstractSerializer<WiredLinkConnectionsMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        WiredLinkConnectionsMessageComposer message
    )
    {
        packet
            .WriteInteger(message.RoomId)
            .WriteInteger(message.FurniId)
            .WriteString(message.NetworkId)
            .WriteInteger(message.Connections.Length);

        foreach (var connection in message.Connections)
        {
            packet
                .WriteInteger(connection.RoomId)
                .WriteString(connection.RoomName)
                .WriteInteger(connection.FurniId)
                .WriteBoolean(connection.IsBuildersClub)
                .WriteBoolean(connection.ReceiveLocal);
        }
    }
}
