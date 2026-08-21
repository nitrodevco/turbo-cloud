using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Game.Directory;

internal class Game2InArenaQueueMessageMessageComposerSerializer(int header)
    : AbstractSerializer<Game2InArenaQueueMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2InArenaQueueMessageMessageComposer message
    )
    {
        //
    }
}
