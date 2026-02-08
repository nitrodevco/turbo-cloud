using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class CatalogPageWithEarliestExpiryMessageComposerSerializer(int header)
    : AbstractSerializer<CatalogPageWithEarliestExpiryMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CatalogPageWithEarliestExpiryMessageComposer message
    )
    {
        //
    }
}
