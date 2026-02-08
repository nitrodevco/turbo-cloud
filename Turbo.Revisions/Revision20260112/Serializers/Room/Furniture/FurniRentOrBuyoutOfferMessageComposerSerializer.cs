using Turbo.Primitives.Messages.Outgoing.Room.Furniture;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Room.Furniture;

internal class FurniRentOrBuyoutOfferMessageComposerSerializer(int header)
    : AbstractSerializer<FurniRentOrBuyoutOfferMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        FurniRentOrBuyoutOfferMessageComposer message
    )
    {
        packet
            .WriteBoolean(message.IsWallItem)
            .WriteString(message.FurniTypeName)
            .WriteBoolean(message.Buyout)
            .WriteInteger(message.PriceInCredits)
            .WriteInteger(message.PriceInActivityPoints)
            .WriteInteger(message.ActivityPointType);
    }
}
