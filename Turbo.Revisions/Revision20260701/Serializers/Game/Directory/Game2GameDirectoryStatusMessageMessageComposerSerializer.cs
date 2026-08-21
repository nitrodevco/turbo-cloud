using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Game.Directory;

internal class Game2GameDirectoryStatusMessageMessageComposerSerializer(int header)
    : AbstractSerializer<Game2GameDirectoryStatusMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2GameDirectoryStatusMessageMessageComposer message
    )
    {
        //
    }
}
