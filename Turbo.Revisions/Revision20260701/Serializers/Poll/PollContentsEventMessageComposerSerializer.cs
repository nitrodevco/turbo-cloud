using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Poll;

internal class PollContentsEventMessageComposerSerializer(int header)
    : AbstractSerializer<PollContentsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PollContentsEventMessageComposer message
    )
    {
        //
    }
}
