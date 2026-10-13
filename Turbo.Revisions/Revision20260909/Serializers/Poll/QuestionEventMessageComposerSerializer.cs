using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Serializers.Poll;

/// <summary>
/// Flash's QuestionParser.as:53: poll type, poll id, question id, duration, then the question (id,
/// number, type, content). Only types 1 and 2 (choices) read on; this composer carries no choices,
/// so those get an empty list. Official captures of type 6 carry 16 more bytes Flash never reads.
/// </summary>
internal class QuestionEventMessageComposerSerializer(int header)
    : AbstractSerializer<QuestionEventMessageComposer>(header)
{
    protected override void Serialize(IServerPacket packet, QuestionEventMessageComposer message)
    {
        packet
            .WriteString(message.PollType)
            .WriteInteger(message.PollId)
            .WriteInteger(message.QuestionId)
            .WriteInteger(message.DurationMs)
            .WriteInteger(message.QuestionId)
            .WriteInteger(message.Number)
            .WriteInteger(message.QuestionType)
            .WriteString(message.Content);

        if (message.QuestionType is 1 or 2)
            packet.WriteInteger(0).WriteInteger(0);
    }
}
