using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Trading;

internal class TradingAcceptEventMessageComposerSerializer(int header)
    : AbstractSerializer<TradingAcceptEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TradingAcceptEventMessageComposer message
    )
    {
        // Accepted travels as an int the client compares with 0.
        packet.WriteInteger(message.PlayerId).WriteInteger(message.Accepted ? 1 : 0);
    }
}
