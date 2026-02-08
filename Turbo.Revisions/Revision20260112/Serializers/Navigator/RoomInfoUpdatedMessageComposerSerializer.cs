using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class RoomInfoUpdatedMessageComposerSerializer(int header)
    : AbstractSerializer<RoomInfoUpdatedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomInfoUpdatedMessageComposer message)
    {
        packet.WriteInteger(message.RoomId);
    }
}
