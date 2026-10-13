using System.Collections.Immutable;
using Turbo.Primitives.Messages.Incoming.Poll;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Packets;

namespace Turbo.Revisions.Revision20260909.Parsers.Poll;

/// <summary>outgoing/poll/PollAnswerComposer.as:9: poll id, question id, count, then each answer.</summary>
internal class PollAnswerMessageParser : IParser
{
    public IMessageEvent Parse(IClientPacket packet)
    {
        var pollId = packet.PopInt();
        var questionId = packet.PopInt();
        var count = packet.PopInt();
        var answers = ImmutableArray.CreateBuilder<string>();

        for (var i = 0; i < count; i++)
            answers.Add(packet.PopString());

        return new PollAnswerMessage
        {
            PollId = pollId,
            QuestionId = questionId,
            Answers = answers.ToImmutable(),
        };
    }
}
