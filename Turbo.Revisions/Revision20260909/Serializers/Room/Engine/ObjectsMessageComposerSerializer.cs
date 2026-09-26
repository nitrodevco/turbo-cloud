using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine;

internal class ObjectsMessageComposerSerializer(int header)
    : AbstractSerializer<ObjectsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ObjectsMessageComposer message)
    {
        OwnerNamesSerializer.Serialize(packet, message.OwnerNames);

        packet.WriteInteger(message.FloorItems.Length);

        foreach (var item in message.FloorItems)
        {
            FloorItemSerializer.Serialize(packet, item);
        }
    }
}
