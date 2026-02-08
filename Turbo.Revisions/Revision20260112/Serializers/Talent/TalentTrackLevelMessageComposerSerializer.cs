using Turbo.Primitives.Messages.Outgoing.Talent;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Talent;

internal class TalentTrackLevelMessageComposerSerializer(int header)
    : AbstractSerializer<TalentTrackLevelMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, TalentTrackLevelMessageComposer message)
    {
        //
    }
}
