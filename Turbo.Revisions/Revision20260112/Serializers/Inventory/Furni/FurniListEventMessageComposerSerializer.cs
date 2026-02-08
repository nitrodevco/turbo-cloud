using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Inventory.Furni.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Furni;

internal class FurniListEventMessageComposerSerializer(int header)
    : AbstractSerializer<FurniListEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, FurniListEventMessageComposer message)
    {
        packet
            .WriteInteger(message.TotalFragments)
            .WriteInteger(message.CurrentFragment)
            .WriteInteger(message.Items.Length);

        foreach (var item in message.Items)
            FurnitureItemSerializer.Serialize(packet, item);
    }
}
