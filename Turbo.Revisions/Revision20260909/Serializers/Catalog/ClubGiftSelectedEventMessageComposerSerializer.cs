using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Catalog.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class ClubGiftSelectedEventMessageComposerSerializer(int header)
    : AbstractSerializer<ClubGiftSelectedEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ClubGiftSelectedEventMessageComposer message
    )
    {
        packet
            .WriteString(message.Offer.LocalizationId)
            .WriteInteger(message.Offer.Products.Length);

        foreach (var product in message.Offer.Products)
            CatalogProductSerializer.Serialize(packet, product);
    }
}
