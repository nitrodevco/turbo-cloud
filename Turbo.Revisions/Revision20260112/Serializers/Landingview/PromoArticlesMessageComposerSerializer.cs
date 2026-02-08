using Turbo.Primitives.Messages.Outgoing.Landingview;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Landingview;

internal class PromoArticlesMessageComposerSerializer(int header)
    : AbstractSerializer<PromoArticlesMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PromoArticlesMessageComposer message)
    {
        //
    }
}
