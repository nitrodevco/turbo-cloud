using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Catalog.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class HabboClubExtendOfferMessageComposerSerializer(int header)
    : AbstractSerializer<HabboClubExtendOfferMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HabboClubExtendOfferMessageComposer message
    ) => ClubOfferSerializer.SerializeExtend(packet, message.Offer);
}
