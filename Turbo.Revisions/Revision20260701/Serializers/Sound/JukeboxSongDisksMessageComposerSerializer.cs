using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Sound;

internal class JukeboxSongDisksMessageComposerSerializer(int header)
    : AbstractSerializer<JukeboxSongDisksMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, JukeboxSongDisksMessageComposer message)
    {
        //
    }
}
