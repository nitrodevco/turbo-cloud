using Turbo.Primitives.Messages.Outgoing.Game.Score;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Game.Score;

internal class Game2WeeklyGroupLeaderboardMessageComposerSerializer(int header)
    : AbstractSerializer<Game2WeeklyGroupLeaderboardMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2WeeklyGroupLeaderboardMessageComposer message
    )
    {
        GameLeaderboardWriter.WriteWeek(packet, message.Leaderboard);
        GameLeaderboardWriter.Write(packet, message.Leaderboard);
        packet.WriteInteger(message.Leaderboard.FavouriteGroupId);
    }
}
