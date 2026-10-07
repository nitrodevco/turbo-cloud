using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Vault.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Vault;

internal class ItemsChestContentsUpdatedMessageComposerSerializer(int header)
    : AbstractSerializer<ItemsChestContentsUpdatedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ItemsChestContentsUpdatedMessageComposer message
    )
    {
        packet.WriteInteger(message.ChestId).WriteInteger(message.RemovedItemIds.Length);

        foreach (var itemId in message.RemovedItemIds)
            packet.WriteInteger(itemId);

        packet.WriteInteger(message.AddedItems.Length);

        foreach (var item in message.AddedItems)
            ChestStorageSerializer.Serialize(packet, item);
    }
}
