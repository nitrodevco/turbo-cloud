using Turbo.Primitives.Messages.Outgoing.Poll;
using Turbo.Primitives.Packets;
using Turbo.Primitives.Rooms.Polls;

namespace Turbo.Revisions.Revision20260909.Serializers.Poll;

/// <summary>
/// Flash's PollContentsParser.as:59 (loop as in the 0909 build, where it decompiles whole): id,
/// start message, end message, the questions each followed by its follow-ups, then the score poll
/// flag. A question's choices follow it only for the two choice types (parseQuestion).
/// </summary>
internal class PollContentsEventMessageComposerSerializer(int header)
    : AbstractSerializer<PollContentsEventMessageComposer>(header)
{
    protected override void Serialize(
        IServerPacket packet,
        PollContentsEventMessageComposer message
    )
    {
        packet
            .WriteInteger(message.PollId)
            .WriteString(message.StartMessage)
            .WriteString(message.EndMessage)
            .WriteInteger(message.Questions.Length);

        foreach (var question in message.Questions)
        {
            WriteQuestion(packet, question);

            packet.WriteInteger(question.Children.Length);

            foreach (var child in question.Children)
                WriteQuestion(packet, child);
        }

        packet.WriteBoolean(message.NpsPoll);
    }

    private static void WriteQuestion(IServerPacket packet, RoomPollQuestionSnapshot question)
    {
        var hasChoices =
            question.QuestionType
            is RoomPollQuestionType.SingleChoice
                or RoomPollQuestionType.MultipleChoice;

        packet
            .WriteInteger(question.QuestionId)
            .WriteInteger(question.SortOrder)
            .WriteInteger((int)question.QuestionType)
            .WriteString(question.Text)
            .WriteInteger(question.Category)
            .WriteInteger(question.AnswerType)
            .WriteInteger(hasChoices ? question.Choices.Length : 0);

        if (!hasChoices)
            return;

        foreach (var choice in question.Choices)
            packet
                .WriteString(choice.Value)
                .WriteString(choice.Text)
                .WriteInteger(choice.ChoiceType);
    }
}
