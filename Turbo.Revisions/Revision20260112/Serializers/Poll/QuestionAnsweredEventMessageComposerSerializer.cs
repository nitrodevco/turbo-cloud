using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Poll;

internal class QuestionAnsweredEventMessageComposerSerializer(int header)
    : AbstractSerializer<QuestionAnsweredEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        QuestionAnsweredEventMessageComposer message
    )
    {
        //
    }
}
