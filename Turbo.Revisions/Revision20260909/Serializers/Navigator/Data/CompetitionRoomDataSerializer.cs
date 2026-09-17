using Turbo.Primitives.Navigator.Snapshots;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Navigator.Data;

internal class CompetitionRoomDataSerializer
{
    public static void Serialize(IServerPacket packet, CompetitionRoomDataSnapshot message)
    {
        packet.WriteInteger(message.GoalId);
        packet.WriteInteger(message.PageIndex);
        packet.WriteInteger(message.PageCount);
    }
}
