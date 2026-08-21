using Turbo.Primitives.Messages.Outgoing.Sound;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Sound;

internal class OfficialSongIdMessageComposerSerializer(int header)
    : AbstractSerializer<OfficialSongIdMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, OfficialSongIdMessageComposer message)
    {
        //
    }
}
