using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Help;

internal class ChatReviewSessionOfferedToGuideMessageComposerSerializer(int header)
    : AbstractSerializer<ChatReviewSessionOfferedToGuideMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        ChatReviewSessionOfferedToGuideMessageComposer message
    )
    {
        //
    }
}
