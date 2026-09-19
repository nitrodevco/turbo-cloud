using Turbo.Primitives.Messages.Outgoing.Inventory.Bots;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Bots.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Bots;

internal class BotInventoryEventMessageComposerSerializer(int header)
    : AbstractSerializer<BotInventoryEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        BotInventoryEventMessageComposer message
    )
    {
        packet.WriteInteger(message.Bots.Length);

        foreach (var bot in message.Bots)
            BotDataSerializer.Serialize(packet, bot);
    }
}
