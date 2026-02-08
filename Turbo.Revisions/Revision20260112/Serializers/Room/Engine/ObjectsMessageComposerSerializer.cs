using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ObjectsMessageComposerSerializer(int header)
    : AbstractSerializer<ObjectsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ObjectsMessageComposer message)
    {
        packet.WriteInteger(message.OwnerNames.Count);

        foreach (var (ownerId, ownerName) in message.OwnerNames)
        {
            packet.WriteInteger(ownerId);
            packet.WriteString(ownerName);
        }

        packet.WriteInteger(message.FloorItems.Length);

        foreach (var item in message.FloorItems)
        {
            FloorItemSerializer.Serialize(packet, item);
        }
    }
}
