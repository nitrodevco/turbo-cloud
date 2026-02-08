using Turbo.Primitives.Messages.Outgoing.Competition;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Competition;

internal class CurrentTimingCodeMessageComposerSerializer(int header)
    : AbstractSerializer<CurrentTimingCodeMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        CurrentTimingCodeMessageComposer message
    )
    {
        //
    }
}
