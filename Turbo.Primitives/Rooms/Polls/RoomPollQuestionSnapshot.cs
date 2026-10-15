using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Rooms.Polls;

/// <summary>
/// One poll question as the client reads it (Flash <c>PollContentsParser.parseQuestion</c>), with
/// its follow-up questions as <see cref="Children"/>.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomPollQuestionSnapshot
{
    [Id(0)]
    public required int QuestionId { get; init; }

    [Id(1)]
    public required int SortOrder { get; init; }

    [Id(2)]
    public required RoomPollQuestionType QuestionType { get; init; }

    [Id(3)]
    public required string Text { get; init; }

    [Id(4)]
    public required int Category { get; init; }

    [Id(5)]
    public required int AnswerType { get; init; }

    [Id(6)]
    public required ImmutableArray<RoomPollChoiceSnapshot> Choices { get; init; }

    [Id(7)]
    public required ImmutableArray<RoomPollQuestionSnapshot> Children { get; init; }
}

/// <summary>One choice of a choice question (Flash <c>PollChoice</c>).</summary>
[GenerateSerializer, Immutable]
public sealed record RoomPollChoiceSnapshot
{
    [Id(0)]
    public required string Value { get; init; }

    [Id(1)]
    public required string Text { get; init; }

    [Id(2)]
    public required int ChoiceType { get; init; }
}
