using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Sound;

internal class NowPlayingMessageComposerSerializer(int header)
    : AbstractSerializer<NowPlayingMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, NowPlayingMessageComposer message)
    {
        //
    }
}
