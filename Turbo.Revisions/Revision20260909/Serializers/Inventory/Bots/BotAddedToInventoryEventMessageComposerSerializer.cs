using Turbo.Primitives.Messages.Outgoing.Inventory.Bots;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Bots.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Bots;

internal class BotAddedToInventoryEventMessageComposerSerializer(int header)
    : AbstractSerializer<BotAddedToInventoryEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BotAddedToInventoryEventMessageComposer message
    )
    {
        BotDataSerializer.Serialize(packet, message.Bot);

        packet.WriteBoolean(message.OpenInventory);
    }
}
