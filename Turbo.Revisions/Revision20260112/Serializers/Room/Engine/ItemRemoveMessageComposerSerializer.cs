using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ItemRemoveMessageComposerSerializer(int header)
    : AbstractSerializer<ItemRemoveMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ItemRemoveMessageComposer message)
    {
        packet.WriteString(message.ObjectId.ToString()).WriteInteger(message.PickerId);
    }
}
