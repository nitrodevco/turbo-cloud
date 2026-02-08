using Turbo.Primitives.Messages.Outgoing.Competition;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Competition;

internal class CompetitionEntrySubmitResultMessageComposerSerializer(int header)
    : AbstractSerializer<CompetitionEntrySubmitResultMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CompetitionEntrySubmitResultMessageComposer message
    )
    {
        //
    }
}
