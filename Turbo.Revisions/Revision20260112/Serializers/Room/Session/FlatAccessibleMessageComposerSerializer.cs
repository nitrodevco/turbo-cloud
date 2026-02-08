using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Session;

internal class FlatAccessibleMessageComposerSerializer(int header)
    : AbstractSerializer<FlatAccessibleMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FlatAccessibleMessageComposer message)
    {
        packet.WriteInteger(message.RoomId).WriteString(message.Username);
    }
}
