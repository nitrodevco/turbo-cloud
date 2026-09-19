using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Inventory.Trading.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Trading;

internal class TradingItemListEventMessageComposerSerializer(int header)
    : AbstractSerializer<TradingItemListEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TradingItemListEventMessageComposer message
    )
    {
        packet.WriteInteger(message.FirstPlayerId).WriteInteger(message.FirstItems.Length);

        foreach (var item in message.FirstItems)
            TradeItemSerializer.Serialize(packet, item);

        packet
            .WriteInteger(message.FirstItems.Length)
            .WriteInteger(message.FirstCredits)
            .WriteInteger(message.SecondPlayerId)
            .WriteInteger(message.SecondItems.Length);

        foreach (var item in message.SecondItems)
            TradeItemSerializer.Serialize(packet, item);

        packet.WriteInteger(message.SecondItems.Length).WriteInteger(message.SecondCredits);
    }
}
