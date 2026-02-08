using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Game.Directory;

internal class Game2GameNotFoundMessageMessageComposerSerializer(int header)
    : AbstractSerializer<Game2GameNotFoundMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2GameNotFoundMessageMessageComposer message
    )
    {
        //
    }
}
