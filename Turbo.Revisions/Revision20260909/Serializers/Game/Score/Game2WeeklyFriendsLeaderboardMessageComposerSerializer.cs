using Turbo.Primitives.Messages.Outgoing.Game.Score;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Game.Score;

internal class Game2WeeklyFriendsLeaderboardMessageComposerSerializer(int header)
    : AbstractSerializer<Game2WeeklyFriendsLeaderboardMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2WeeklyFriendsLeaderboardMessageComposer message
    )
    {
        GameLeaderboardWriter.WriteWeek(packet, message.Leaderboard);
        GameLeaderboardWriter.Write(packet, message.Leaderboard);
    }
}
