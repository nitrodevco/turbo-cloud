using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Poll;

internal class QuestionAnsweredEventMessageComposerSerializer(int header)
    : AbstractSerializer<QuestionAnsweredEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        QuestionAnsweredEventMessageComposer message
    )
    {
        packet
            .WriteInteger(message.UserId)
            .WriteString(message.Value)
            .WriteInteger(message.AnswerCounts.Count);

        foreach (var (value, count) in message.AnswerCounts)
            packet.WriteString(value).WriteInteger(count);
    }
}
