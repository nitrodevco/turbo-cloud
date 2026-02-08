using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Game.Directory;

internal class Game2StopCounterMessageMessageComposerSerializer(int header)
    : AbstractSerializer<Game2StopCounterMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2StopCounterMessageMessageComposer message
    )
    {
        //
    }
}
