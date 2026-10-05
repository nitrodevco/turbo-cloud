using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Catalog;

internal class CatalogPublishedMessageComposerSerializer(int header)
    : AbstractSerializer<CatalogPublishedMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CatalogPublishedMessageComposer message)
    {
        packet.WriteBoolean(message.InstantlyRefreshCatalogue);

        // Read only when there is more: no hash, nothing written.
        if (message.NewFurniDataHash is { } hash)
            packet.WriteString(hash);
    }
}
