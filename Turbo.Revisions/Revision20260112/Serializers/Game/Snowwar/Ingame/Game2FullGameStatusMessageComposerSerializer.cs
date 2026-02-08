using Turbo.Primitives.Messages.Outgoing.Game.Snowwar.Ingame;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Game.Snowwar.Ingame;

internal class Game2FullGameStatusMessageComposerSerializer(int header)
    : AbstractSerializer<Game2FullGameStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2FullGameStatusMessageComposer message
    )
    {
        //
    }
}
