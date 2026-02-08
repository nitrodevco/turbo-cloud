using Turbo.Primitives.Messages.Outgoing.Talent;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Talent;

internal class TalentTrackMessageComposerSerializer(int header)
    : AbstractSerializer<TalentTrackMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, TalentTrackMessageComposer message)
    {
        //
    }
}
