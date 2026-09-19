using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Trading;

internal class TradingOpenEventMessageComposerSerializer(int header)
    : AbstractSerializer<TradingOpenEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, TradingOpenEventMessageComposer message)
    {
        // The client reads the "can trade" flags as ints and compares them with 1.
        packet
            .WriteInteger(message.PlayerId)
            .WriteInteger(message.PlayerCanTrade ? 1 : 0)
            .WriteInteger(message.OtherPlayerId)
            .WriteInteger(message.OtherPlayerCanTrade ? 1 : 0);
    }
}
