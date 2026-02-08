using Turbo.Primitives.Packets;
using Turbo.Primitives.Snapshots.Navigator;

namespace Turbo.Revisions.Revision20260112.Serializers.Navigator.Data;

internal class CompetitionRoomDataSerializer
{
    public static void Serialize(IServerPacket packet, CompetitionRoomDataSnapshot message)
    {
        packet.WriteInteger(message.GoalId);
        packet.WriteInteger(message.PageIndex);
        packet.WriteInteger(message.PageCount);
    }
}
