using System.Collections.Immutable;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Incoming.Poll;

/// <summary>Flash <c>PollAnswerComposer</c>: the poll, the question and one string per answer.</summary>
public record PollAnswerMessage : IMessageEvent
{
    public required int PollId { get; init; }
    public required int QuestionId { get; init; }
    public required ImmutableArray<string> Answers { get; init; }
}
