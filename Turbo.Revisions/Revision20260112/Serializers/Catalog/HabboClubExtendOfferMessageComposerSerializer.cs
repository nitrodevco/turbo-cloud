using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class HabboClubExtendOfferMessageComposerSerializer(int header)
    : AbstractSerializer<HabboClubExtendOfferMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        HabboClubExtendOfferMessageComposer message
    )
    {
        //
    }
}
