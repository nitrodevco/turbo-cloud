using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Poll;

internal class QuestionFinishedEventMessageComposerSerializer(int header)
    : AbstractSerializer<QuestionFinishedEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        QuestionFinishedEventMessageComposer message
    )
    {
        //
    }
}
