using Turbo.Primitives.Messages.Outgoing.Competition;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Competition;

internal class CompetitionVotingInfoMessageComposerSerializer(int header)
    : AbstractSerializer<CompetitionVotingInfoMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CompetitionVotingInfoMessageComposer message
    )
    {
        //
    }
}
