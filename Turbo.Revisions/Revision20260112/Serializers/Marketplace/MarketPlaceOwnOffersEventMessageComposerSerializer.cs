using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Marketplace;

internal class MarketPlaceOwnOffersEventMessageComposerSerializer(int header)
    : AbstractSerializer<MarketPlaceOwnOffersEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketPlaceOwnOffersEventMessageComposer message
    )
    {
        //
    }
}
