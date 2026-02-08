using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260112.Serializers.Poll;

internal class QuestionEventMessageComposerSerializer(int header)
    : AbstractSerializer<QuestionEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuestionEventMessageComposer message)
    {
        //
    }
}
