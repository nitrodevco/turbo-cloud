using Turbo.Primitives.Messages.Outgoing.Competition;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Competition;

internal class IsUserPartOfCompetitionMessageComposerSerializer(int header)
    : AbstractSerializer<IsUserPartOfCompetitionMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        IsUserPartOfCompetitionMessageComposer message
    )
    {
        //
    }
}
