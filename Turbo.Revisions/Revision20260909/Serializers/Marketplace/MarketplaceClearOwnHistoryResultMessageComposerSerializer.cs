using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Marketplace;

internal class MarketplaceClearOwnHistoryResultMessageComposerSerializer(int header)
    : AbstractSerializer<MarketplaceClearOwnHistoryResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketplaceClearOwnHistoryResultMessageComposer message
    )
    {
        packet.WriteBoolean(message.Success);
    }
}
