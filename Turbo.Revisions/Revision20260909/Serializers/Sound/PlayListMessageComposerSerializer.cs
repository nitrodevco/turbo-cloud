using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;
using Turbo.Revisions.Revision20260909.Serializers.Sound.Data;

namespace Turbo.Revisions.Revision20260909.Serializers.Sound;

internal class PlayListMessageComposerSerializer(int header)
    : AbstractSerializer<PlayListMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PlayListMessageComposer message)
    {
        packet.WriteInteger(message.SynchronizationCountMs).WriteInteger(message.Songs.Length);

        foreach (var song in message.Songs)
            PlayListEntrySerializer.Serialize(packet, song);
    }
}
