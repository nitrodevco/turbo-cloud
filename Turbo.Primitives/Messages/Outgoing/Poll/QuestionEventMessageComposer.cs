using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Poll;

/// <summary>A question put to the room (Flash parser poll/QuestionParser.as:53).</summary>
[GenerateSerializer, Immutable]
public sealed record QuestionEventMessageComposer : IComposer
{
    [Id(0)]
    public required string PollType { get; init; }

    [Id(1)]
    public required int PollId { get; init; }

    [Id(2)]
    public required int QuestionId { get; init; }

    /// <summary>How long the room has to answer, in milliseconds.</summary>
    [Id(3)]
    public required int DurationMs { get; init; }

    /// <summary>The question's position in its poll.</summary>
    [Id(4)]
    public required int Number { get; init; }

    [Id(5)]
    public required int QuestionType { get; init; }

    [Id(6)]
    public required string Content { get; init; }
}
