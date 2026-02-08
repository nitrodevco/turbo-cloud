using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Poll;

internal class PollOfferEventMessageComposerSerializer(int header)
    : AbstractSerializer<PollOfferEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, PollOfferEventMessageComposer message)
    {
        //
    }
}
