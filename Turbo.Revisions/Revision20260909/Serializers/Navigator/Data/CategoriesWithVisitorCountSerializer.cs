using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Navigator.Data;

internal class CategoriesWithVisitorCountSerializer
{
    public static void Serialize(IServerPacket packet, CategoriesWithVisitorCountSnapshot message)
    {
        packet.WriteInteger(message.CategoriesWithVisitorCount.Count);

        foreach (var category in message.CategoriesWithVisitorCount)
        {
            packet
                .WriteInteger(category.Key)
                .WriteInteger(category.Value[0])
                .WriteInteger(category.Value[1]);
        }
    }
}
