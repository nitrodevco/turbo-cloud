using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Help;

internal class ChatReviewSessionDetachedMessageComposerSerializer(int header)
    : AbstractSerializer<ChatReviewSessionDetachedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ChatReviewSessionDetachedMessageComposer message
    )
    {
        //
    }
}
