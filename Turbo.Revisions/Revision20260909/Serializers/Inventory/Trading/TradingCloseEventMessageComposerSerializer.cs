using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Inventory.Trading;

internal class TradingCloseEventMessageComposerSerializer(int header)
    : AbstractSerializer<TradingCloseEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TradingCloseEventMessageComposer message
    )
    {
        packet.WriteInteger(message.PlayerId).WriteInteger((int)message.Reason);
    }
}
