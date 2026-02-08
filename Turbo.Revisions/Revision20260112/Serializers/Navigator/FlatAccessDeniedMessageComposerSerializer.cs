using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class FlatAccessDeniedMessageComposerSerializer(int header)
    : AbstractSerializer<FlatAccessDeniedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FlatAccessDeniedMessageComposer message)
    {
        packet.WriteInteger(message.RoomId).WriteString(message.Username);
    }
}
