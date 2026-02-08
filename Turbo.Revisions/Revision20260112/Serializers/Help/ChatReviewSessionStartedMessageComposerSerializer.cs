using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Help;

internal class ChatReviewSessionStartedMessageComposerSerializer(int header)
    : AbstractSerializer<ChatReviewSessionStartedMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ChatReviewSessionStartedMessageComposer message
    )
    {
        //
    }
}
