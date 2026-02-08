using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Inventory.Furni.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Furni;

internal class FurniListAddOrUpdateEventMessageComposerSerializer(int header)
    : AbstractSerializer<FurniListAddOrUpdateEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FurniListAddOrUpdateEventMessageComposer message
    )
    {
        FurnitureItemSerializer.Serialize(packet, message.Item);
    }
}
