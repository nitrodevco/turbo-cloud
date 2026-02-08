using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Trading;

internal class TradeSilverFeeMessageComposerSerializer(int header)
    : AbstractSerializer<TradeSilverFeeMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, TradeSilverFeeMessageComposer message)
    {
        //
    }
}
