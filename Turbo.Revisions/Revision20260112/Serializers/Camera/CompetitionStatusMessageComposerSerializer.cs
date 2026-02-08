using Turbo.Primitives.Messages.Outgoing.Camera;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Camera;

internal class CompetitionStatusMessageComposerSerializer(int header)
    : AbstractSerializer<CompetitionStatusMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CompetitionStatusMessageComposer message
    )
    {
        //
    }
}
