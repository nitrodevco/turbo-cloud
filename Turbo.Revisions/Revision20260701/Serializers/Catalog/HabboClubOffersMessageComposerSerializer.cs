using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Catalog;

internal class HabboClubOffersMessageComposerSerializer(int header)
    : AbstractSerializer<HabboClubOffersMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, HabboClubOffersMessageComposer message)
    {
        //
    }
}
