using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Game.Directory;

internal class Game2AccountGameStatusMessageMessageComposerSerializer(int header)
    : AbstractSerializer<Game2AccountGameStatusMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2AccountGameStatusMessageMessageComposer message
    )
    {
        //
    }
}
