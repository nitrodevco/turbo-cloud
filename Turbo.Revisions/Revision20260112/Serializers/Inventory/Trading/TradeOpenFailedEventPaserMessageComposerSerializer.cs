using Turbo.Primitives.Messages.Outgoing.Inventory.Trading;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Inventory.Trading;

internal class TradeOpenFailedEventPaserMessageComposerSerializer(int header)
    : AbstractSerializer<TradeOpenFailedEventPaserMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        TradeOpenFailedEventPaserMessageComposer message
    )
    {
        //
    }
}
