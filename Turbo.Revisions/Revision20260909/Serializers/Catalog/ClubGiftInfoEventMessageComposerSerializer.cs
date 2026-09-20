using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Catalog.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class ClubGiftInfoEventMessageComposerSerializer(int header)
    : AbstractSerializer<ClubGiftInfoEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ClubGiftInfoEventMessageComposer message
    )
    {
        packet
            .WriteInteger(message.Info.DaysUntilNextGift)
            .WriteInteger(message.Info.GiftsAvailable)
            .WriteInteger(message.Info.Offers.Length);

        foreach (var offer in message.Info.Offers)
            CatalogOfferSerializer.Serialize(packet, offer);

        packet.WriteInteger(message.Info.Gifts.Length);

        foreach (var gift in message.Info.Gifts)
        {
            packet
                .WriteInteger(gift.OfferId)
                .WriteBoolean(gift.IsVip)
                .WriteInteger(gift.DaysRequired)
                .WriteBoolean(gift.IsSelectable);
        }
    }
}
