using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Marketplace;

/// <summary>Flash parser <c>_-21H</c> (MarketplaceCancelAllOffersResult): the offer ids taken back, then whether it worked.</summary>
internal class MarketplaceCancelAllOffersResultMessageComposerSerializer(int header)
    : AbstractSerializer<MarketplaceCancelAllOffersResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketplaceCancelAllOffersResultMessageComposer message
    )
    {
        packet.WriteInteger(message.OfferIds.Length);

        foreach (var offerId in message.OfferIds)
            packet.WriteInteger(offerId);

        packet.WriteBoolean(message.Success);
    }
}
