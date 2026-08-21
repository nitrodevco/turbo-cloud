using Turbo.Primitives.Messages.Outgoing.Help;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260701.Serializers.Help;

internal class QuizResultsMessageComposerSerializer(int header)
    : AbstractSerializer<QuizResultsMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuizResultsMessageComposer message)
    {
        //
    }
}
