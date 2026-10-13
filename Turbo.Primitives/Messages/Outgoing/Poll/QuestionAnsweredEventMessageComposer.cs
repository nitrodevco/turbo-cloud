using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Poll;

/// <summary>
/// Someone in the room answered (Flash parser poll/QuestionAnsweredParser.as:37): their player id,
/// their answer and the counts so far.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record QuestionAnsweredEventMessageComposer : IComposer
{
    [Id(0)]
    public required int UserId { get; init; }

    [Id(1)]
    public required string Value { get; init; }

    [Id(2)]
    public required IReadOnlyList<KeyValuePair<string, int>> AnswerCounts { get; init; }
}
