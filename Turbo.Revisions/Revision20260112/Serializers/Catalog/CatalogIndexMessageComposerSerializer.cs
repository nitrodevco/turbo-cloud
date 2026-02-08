using System.Collections.Generic;
using Turbo.Primitives.Catalog.Enums;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Messages.Outgoing.Catalog;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Catalog.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog;

internal class CatalogIndexMessageComposerSerializer(int header)
    : AbstractSerializer<CatalogIndexMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, CatalogIndexMessageComposer message)
    {
        var rootPage = message.Catalog.PagesById[message.Catalog.RootPageId];

        if (rootPage is null)
            return;

        SerializePage(packet, message.Catalog, rootPage);

        packet
            .WriteBoolean(message.NewAdditionsAvailable)
            .WriteString(message.Catalog.CatalogType.ToLegacyString());
    }

    private static void SerializePage(
        IServerPacket packet,
        CatalogSnapshot snapshot,
        CatalogPageSnapshot page
    )
    {
        CatalogPageSnapshotSerializer.Serialize(packet, page);

        packet.WriteInteger(page.OfferIds.Length);

        foreach (var offerId in page.OfferIds)
            packet.WriteInteger(offerId);

        List<CatalogPageSnapshot> children = [];

        foreach (var childId in page.ChildIds)
        {
            if (snapshot.PagesById.TryGetValue(childId, out var childPage))
                children.Add(childPage);
        }

        packet.WriteInteger(children.Count);

        foreach (var child in children)
            SerializePage(packet, snapshot, child);
    }
}
