using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Sound;

internal class TraxSongInfoMessageComposerSerializer(int header)
    : AbstractSerializer<TraxSongInfoMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, TraxSongInfoMessageComposer message)
    {
        //
    }
}
