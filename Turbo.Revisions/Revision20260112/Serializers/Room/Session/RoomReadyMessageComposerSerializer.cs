using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Session;

internal class RoomReadyMessageComposerSerializer(int header)
    : AbstractSerializer<RoomReadyMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomReadyMessageComposer message)
    {
        packet.WriteString(message.WorldType).WriteInteger(message.RoomId);
    }
}
