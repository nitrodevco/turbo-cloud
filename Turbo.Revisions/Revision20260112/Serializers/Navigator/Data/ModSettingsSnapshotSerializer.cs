using Turbo.Primitives.Orleans.Snapshots.Room.Settings;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator.Data;

internal class ModSettingsSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, ModSettingsSnapshot message)
    {
        packet.WriteInteger((int)message.WhoCanMute);
        packet.WriteInteger((int)message.WhoCanKick);
        packet.WriteInteger((int)message.WhoCanBan);
    }
}
