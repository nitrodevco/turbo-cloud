using Turbo.Primitives.Messages.Outgoing.Marketplace;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Marketplace;

internal class MarketplaceBuyOfferResultEventMessageComposerSerializer(int header)
    : AbstractSerializer<MarketplaceBuyOfferResultEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        MarketplaceBuyOfferResultEventMessageComposer message
    )
    {
        packet
            .WriteInteger((int)message.Result)
            .WriteInteger(message.OfferId)
            .WriteInteger(message.NewPrice)
            .WriteInteger(message.RequestedOfferId);
    }
}
