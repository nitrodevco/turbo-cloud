using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Sound;

internal class PlayListMessageComposerSerializer(int header)
    : AbstractSerializer<PlayListMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PlayListMessageComposer message)
    {
        //
    }
}
