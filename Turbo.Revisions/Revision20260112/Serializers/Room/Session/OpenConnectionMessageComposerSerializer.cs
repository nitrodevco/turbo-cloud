using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Session;

internal class OpenConnectionMessageComposerSerializer(int header)
    : AbstractSerializer<OpenConnectionMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, OpenConnectionMessageComposer message)
    {
        packet.WriteInteger(message.RoomId);
    }
}
