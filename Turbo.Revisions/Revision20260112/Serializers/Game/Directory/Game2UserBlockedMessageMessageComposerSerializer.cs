using Turbo.Primitives.Messages.Outgoing.Game.Directory;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Game.Directory;

internal class Game2UserBlockedMessageMessageComposerSerializer(int header)
    : AbstractSerializer<Game2UserBlockedMessageMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        Game2UserBlockedMessageMessageComposer message
    )
    {
        //
    }
}
