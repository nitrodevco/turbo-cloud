using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog.Data;

internal class CatalogPageSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, CatalogPageSnapshot message)
    {
        packet
            .WriteBoolean(message.Visible)
            .WriteInteger(message.Icon)
            .WriteInteger(message.Id)
            .WriteString(message.Name)
            .WriteString(message.Localization);
    }
}
