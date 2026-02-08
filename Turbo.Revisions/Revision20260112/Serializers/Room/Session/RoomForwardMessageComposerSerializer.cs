using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Session;

internal class RoomForwardMessageComposerSerializer(int header)
    : AbstractSerializer<RoomForwardMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomForwardMessageComposer message)
    {
        packet.WriteInteger(message.RoomId);
    }
}
