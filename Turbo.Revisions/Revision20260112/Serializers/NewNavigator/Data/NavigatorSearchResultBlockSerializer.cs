using Turbo.Primitives.Orleans.Snapshots.Navigator;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260112.Serializers.Navigator.Data;

namespace Turbo.Revisions.Revision20260112.Serializers.NewNavigator.Data;

internal class NavigatorSearchResultBlockSerializer
{
    public static void Serialize(IServerPacket packet, NavigatorSearchResultBlockSnapshot message)
    {
        packet
            .WriteString(message.SearchCode)
            .WriteString(message.Text)
            .WriteInteger((int)message.ActionAllowed)
            .WriteBoolean(message.ForceClosed)
            .WriteInteger((int)message.ViewMode)
            .WriteInteger(message.Results.Length);

        foreach (var result in message.Results)
        {
            RoomSettingsSerializer.Serialize(packet, result);
        }
    }
}
