using Turbo.Primitives.Messages.Outgoing.Room.Session;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Room.Session;

internal class RoomQueueStatusMessageComposerSerializer(int header)
    : AbstractSerializer<RoomQueueStatusMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, RoomQueueStatusMessageComposer message)
    {
        //
    }
}
