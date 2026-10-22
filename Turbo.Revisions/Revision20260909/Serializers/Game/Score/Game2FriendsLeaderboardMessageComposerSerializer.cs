using Turbo.Primitives.Messages.Outgoing.Game.Score;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Game.Score;

internal class Game2FriendsLeaderboardMessageComposerSerializer(int header)
    : AbstractSerializer<Game2FriendsLeaderboardMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2FriendsLeaderboardMessageComposer message
    )
    {
        GameLeaderboardWriter.Write(packet, message.Leaderboard);
    }
}
