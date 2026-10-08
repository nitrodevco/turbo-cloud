using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Sound.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Sound;

internal class PlayListSongAddedMessageComposerSerializer(int header)
    : AbstractSerializer<PlayListSongAddedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PlayListSongAddedMessageComposer message
    ) => PlayListEntrySerializer.Serialize(packet, message.Song);
}
