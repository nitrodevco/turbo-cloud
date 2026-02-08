using Turbo.Primitives.Messages.Outgoing.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator;

internal class CanCreateRoomEventMessageComposerSerializer(int header)
    : AbstractSerializer<CanCreateRoomEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CanCreateRoomEventMessageComposer message
    )
    {
        packet.WriteBoolean(message.CanCreateEvent).WriteInteger(message.ErrorCode);
    }
}
