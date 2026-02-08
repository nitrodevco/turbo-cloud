using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class CanCreateRoomMessageComposerSerializer(int header)
    : AbstractSerializer<CanCreateRoomMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CanCreateRoomMessageComposer message)
    {
        packet.WriteInteger(message.ResultCode).WriteInteger(message.RoomLimit);
    }
}
