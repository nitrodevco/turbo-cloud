using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Revisions.Revision20260701.Serializers.Navigator.Data;

internal class ModSettingsSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, ModSettingsSnapshot message)
    {
        packet.WriteInteger((int)message.WhoCanMute);
        packet.WriteInteger((int)message.WhoCanKick);
        packet.WriteInteger((int)message.WhoCanBan);
    }
}
