using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Catalog.Data;

internal class CatalogPageLocalizationSerializer
{
    public static void Serialize(IServerPacket packet, CatalogPageSnapshot message)
    {
        packet.WriteInteger(message.ImageData.Count);

        foreach (var data in message.ImageData)
            packet.WriteString(data);

        packet.WriteInteger(message.TextData.Count);

        foreach (var data in message.TextData)
            packet.WriteString(data);
    }
}
