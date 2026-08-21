using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Help;

internal class QuizDataMessageComposerSerializer(int header)
    : AbstractSerializer<QuizDataMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuizDataMessageComposer message)
    {
        //
    }
}
