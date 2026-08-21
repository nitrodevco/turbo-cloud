using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Game.Directory;

internal class Game2GameCancelledMessageMessageComposerSerializer(int header)
    : AbstractSerializer<Game2GameCancelledMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2GameCancelledMessageMessageComposer message
    )
    {
        //
    }
}
