using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Sound;

internal class PlayListSongAddedMessageComposerSerializer(int header)
    : AbstractSerializer<PlayListSongAddedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PlayListSongAddedMessageComposer message
    )
    {
        //
    }
}
