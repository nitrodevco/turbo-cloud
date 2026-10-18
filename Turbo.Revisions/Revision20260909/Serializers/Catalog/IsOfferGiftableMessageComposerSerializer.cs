using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class IsOfferGiftableMessageComposerSerializer(int header)
    : AbstractSerializer<IsOfferGiftableMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, IsOfferGiftableMessageComposer message)
    {
        packet.WriteInteger(message.OfferId).WriteBoolean(message.IsGiftable);
    }
}
