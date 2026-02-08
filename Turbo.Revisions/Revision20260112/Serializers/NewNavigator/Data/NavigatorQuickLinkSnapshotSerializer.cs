using Turbo.Primitives.Orleans.Snapshots.Navigator;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.NewNavigator.Data;

internal class NavigatorQuickLinkSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, NavigatorQuickLinkSnapshot message)
    {
        packet
            .WriteInteger(message.Id)
            .WriteString(message.SearchCode)
            .WriteString(message.Filter)
            .WriteString(message.Localization);
    }
}
