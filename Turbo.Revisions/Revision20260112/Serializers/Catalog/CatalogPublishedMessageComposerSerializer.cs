using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class CatalogPublishedMessageComposerSerializer(int header)
    : AbstractSerializer<CatalogPublishedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CatalogPublishedMessageComposer message)
    {
        //
    }
}
