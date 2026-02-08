using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Marketplace;

internal class MarketplaceCancelOfferResultEventMessageComposerSerializer(int header)
    : AbstractSerializer<MarketplaceCancelOfferResultEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketplaceCancelOfferResultEventMessageComposer message
    )
    {
        //
    }
}
