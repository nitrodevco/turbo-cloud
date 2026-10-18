using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Room.Engine;

internal class ItemRemoveMultipleMessageComposerSerializer(int header)
    : AbstractSerializer<ItemRemoveMultipleMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ItemRemoveMultipleMessageComposer message
    )
    {
        // Int ids here, where a single ItemRemove sends its id as a string.
        packet.WriteInteger(message.ObjectIdsToRemove.Length);

        foreach (var objectId in message.ObjectIdsToRemove)
            packet.WriteInteger((int)objectId);

        packet.WriteInteger(message.PickerId).WriteBoolean(message.ByInvisibleFurniManager);
    }
}
