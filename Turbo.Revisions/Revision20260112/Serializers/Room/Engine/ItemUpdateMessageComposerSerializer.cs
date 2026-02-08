using Turbo.Primitives.Messages.Outgoing.Room.Engine;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Room.Engine.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Engine;

internal class ItemUpdateMessageComposerSerializer(int header)
    : AbstractSerializer<ItemUpdateMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, ItemUpdateMessageComposer message)
    {
        WallItemSerializer.Serialize(packet, message.WallItem);
    }
}
