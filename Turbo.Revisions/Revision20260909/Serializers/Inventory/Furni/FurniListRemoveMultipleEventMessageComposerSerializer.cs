using Turbo.Primitives.Messages.Outgoing.Inventory.Furni;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Furni;

internal class FurniListRemoveMultipleEventMessageComposerSerializer(int header)
    : AbstractSerializer<FurniListRemoveMultipleEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FurniListRemoveMultipleEventMessageComposer message
    )
    {
        packet.WriteInteger(message.ItemIds.Length);

        foreach (var itemId in message.ItemIds)
            packet.WriteInteger(itemId);
    }
}
