using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Catalog.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class PurchaseOKMessageComposerSerializer(int header)
    : AbstractSerializer<PurchaseOKMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PurchaseOKMessageComposer message)
    {
        CatalogOfferSerializer.SerializeAsPurchased(packet, message.Offer);

        // The official server follows the offer with the furni bought, floor then wall, which
        // the Flash client (PurchaseOKMessageOfferData) does not read.
        packet.WriteInteger(message.FloorItemIds.Length);

        foreach (var itemId in message.FloorItemIds)
            packet.WriteInteger(itemId.Value);

        packet.WriteInteger(message.WallItemIds.Length);

        foreach (var itemId in message.WallItemIds)
            packet.WriteInteger(itemId.Value);
    }
}
