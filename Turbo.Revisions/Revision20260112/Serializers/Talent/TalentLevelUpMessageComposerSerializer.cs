using Turbo.Primitives.Messages.Outgoing.Talent;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Talent;

internal class TalentLevelUpMessageComposerSerializer(int header)
    : AbstractSerializer<TalentLevelUpMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, TalentLevelUpMessageComposer message)
    {
        //
    }
}
