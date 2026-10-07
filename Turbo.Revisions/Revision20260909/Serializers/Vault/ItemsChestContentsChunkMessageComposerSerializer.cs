using Turbo.Primitives.Messages.Outgoing.Vault;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Vault.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Vault;

internal class ItemsChestContentsChunkMessageComposerSerializer(int header)
    : AbstractSerializer<ItemsChestContentsChunkMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ItemsChestContentsChunkMessageComposer message
    )
    {
        packet
            .WriteInteger(message.ChestId)
            .WriteInteger(message.TotalFragments)
            .WriteInteger(message.FragmentNo)
            .WriteInteger(message.Items.Length);

        foreach (var item in message.Items)
            ChestStorageSerializer.Serialize(packet, item);
    }
}
