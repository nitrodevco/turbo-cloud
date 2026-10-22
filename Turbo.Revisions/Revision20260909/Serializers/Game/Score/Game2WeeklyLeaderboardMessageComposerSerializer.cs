using Turbo.Primitives.Messages.Outgoing.Game.Score;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Game.Score;

internal class Game2WeeklyLeaderboardMessageComposerSerializer(int header)
    : AbstractSerializer<Game2WeeklyLeaderboardMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2WeeklyLeaderboardMessageComposer message
    )
    {
        GameLeaderboardWriter.WriteWeek(packet, message.Leaderboard);
        GameLeaderboardWriter.Write(packet, message.Leaderboard);
    }
}
