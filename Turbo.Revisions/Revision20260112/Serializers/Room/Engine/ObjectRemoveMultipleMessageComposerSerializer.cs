using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ObjectRemoveMultipleMessageComposerSerializer(int header)
    : AbstractSerializer<ObjectRemoveMultipleMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ObjectRemoveMultipleMessageComposer message
    )
    {
        packet.WriteInteger(message.ObjectIdsToRemove.Length);

        foreach (var objectId in message.ObjectIdsToRemove)
            packet.WriteInteger((int)objectId);

        packet.WriteInteger(message.PickerId);
    }
}
