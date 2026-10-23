using Turbo.Primitives.Messages.Outgoing.Game.Score;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Game.Score;

internal class Game2TotalLeaderboardMessageComposerSerializer(int header)
    : AbstractSerializer<Game2TotalLeaderboardMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2TotalLeaderboardMessageComposer message
    )
    {
        GameLeaderboardWriter.Write(packet, message.Leaderboard);
    }
}
