using Turbo.Primitives.Orleans.Snapshots.Room.Settings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator.Data;

internal class RoomChatSettingsSerializer
{
    public static void Serialize(IServerPacket packet, ChatSettingsSnapshot message)
    {
        packet.WriteInteger((int)message.ChatMode);
        packet.WriteInteger((int)message.BubbleWidth);
        packet.WriteInteger((int)message.ScrollSpeed);
        packet.WriteInteger(message.FullHearRange);
        packet.WriteInteger((int)message.FloodSensitivity);
    }
}
