using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ObjectUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<ObjectUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ObjectUpdateMessageComposer message)
    {
        FloorItemSerializer.Serialize(packet, message.FloorItem);
    }
}
