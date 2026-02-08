using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ObjectDataUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<ObjectDataUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ObjectDataUpdateMessageComposer message)
    {
        packet.WriteString(message.ObjectId.ToString());

        StuffDataSnapshotSerializer.Serialize(packet, message.StuffData);
    }
}
