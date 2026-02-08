using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ObjectAddMessageComposerSerializer(int header)
    : AbstractSerializer<ObjectAddMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ObjectAddMessageComposer message)
    {
        FloorItemSerializer.Serialize(packet, message.FloorItem);

        packet.WriteString(message.FloorItem.OwnerName);
    }
}
