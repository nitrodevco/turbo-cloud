using Turbo.Primitives.Messages.Outgoing.Game.Score;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Game.Score;

internal class Game2TotalGroupLeaderboardMessageComposerSerializer(int header)
    : AbstractSerializer<Game2TotalGroupLeaderboardMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2TotalGroupLeaderboardMessageComposer message
    )
    {
        GameLeaderboardWriter.Write(packet, message.Leaderboard);
        packet.WriteInteger(message.Leaderboard.FavouriteGroupId);
    }
}
