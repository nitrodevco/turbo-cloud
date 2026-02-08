using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class ConvertedRoomIdMessageComposerSerializer(int header)
    : AbstractSerializer<ConvertedRoomIdMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ConvertedRoomIdMessageComposer message)
    {
        packet.WriteString(message.GlobalId).WriteInteger(message.ConvertedId);
    }
}
