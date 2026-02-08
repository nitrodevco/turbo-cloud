using Turbo.Primitives.Messages.Outgoing.Game.Snowwar.Arena;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Game.Snowwar.Arena;

internal class Game2GameChatFromPlayerMessageComposerSerializer(int header)
    : AbstractSerializer<Game2GameChatFromPlayerMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2GameChatFromPlayerMessageComposer message
    )
    {
        //
    }
}
