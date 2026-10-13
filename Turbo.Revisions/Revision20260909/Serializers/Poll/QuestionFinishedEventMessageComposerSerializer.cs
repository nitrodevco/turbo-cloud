using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Poll;

internal class QuestionFinishedEventMessageComposerSerializer(int header)
    : AbstractSerializer<QuestionFinishedEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        QuestionFinishedEventMessageComposer message
    )
    {
        packet.WriteInteger(message.QuestionId).WriteInteger(message.AnswerCounts.Count);

        foreach (var (value, count) in message.AnswerCounts)
            packet.WriteString(value).WriteInteger(count);
    }
}
