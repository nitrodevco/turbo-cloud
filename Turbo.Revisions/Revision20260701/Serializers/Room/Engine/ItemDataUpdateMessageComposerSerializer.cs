using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Room.Engine;

internal class ItemDataUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<ItemDataUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ItemDataUpdateMessageComposer message)
    {
        packet.WriteString(message.ObjectId.ToString()).WriteString(message.State);
    }
}
