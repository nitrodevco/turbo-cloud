using Turbo.Primitives.Messages.Outgoing.Game.Score;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Game.Score;

internal class Game2GroupLeaderboardMessageComposerSerializer(int header)
    : AbstractSerializer<Game2GroupLeaderboardMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2GroupLeaderboardMessageComposer message
    )
    {
        //
    }
}
