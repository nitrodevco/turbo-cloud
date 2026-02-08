using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class FlatCreatedMessageComposerSerializer(int header)
    : AbstractSerializer<FlatCreatedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FlatCreatedMessageComposer message)
    {
        packet.WriteInteger(message.RoomId).WriteString(message.Name);
    }
}
