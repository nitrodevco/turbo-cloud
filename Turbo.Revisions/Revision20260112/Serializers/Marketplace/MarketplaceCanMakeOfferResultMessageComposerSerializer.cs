using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Marketplace;

internal class MarketplaceCanMakeOfferResultMessageComposerSerializer(int header)
    : AbstractSerializer<MarketplaceCanMakeOfferResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketplaceCanMakeOfferResultMessageComposer message
    )
    {
        //
    }
}
