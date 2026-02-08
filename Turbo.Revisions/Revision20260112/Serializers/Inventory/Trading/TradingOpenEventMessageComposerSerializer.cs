using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Trading;

internal class TradingOpenEventMessageComposerSerializer(int header)
    : AbstractSerializer<TradingOpenEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, TradingOpenEventMessageComposer message)
    {
        //
    }
}
