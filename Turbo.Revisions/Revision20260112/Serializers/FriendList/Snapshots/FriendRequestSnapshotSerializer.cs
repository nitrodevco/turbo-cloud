using Turbo.Primitives.Packets;
using Turbo.Primitives.Players.Messenger;

namespace Turbo.Revisions.Revision20260112.Serializers.FriendList.Snapshots;

internal class FriendRequestSnapshotSerializer
{
    public static void Serialize(IServerPacket packet, MessengerRequestDto message)
    {
        packet
            .WriteInteger(message.RequesterPlayerId)
            .WriteString(message.RequesterName)
            .WriteString(message.RequesterFigure);
    }
}
