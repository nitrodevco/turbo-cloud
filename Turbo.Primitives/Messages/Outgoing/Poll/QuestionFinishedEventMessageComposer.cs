using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Poll;

/// <summary>The question's time is up (Flash parser poll/QuestionFinishedParser.as:29): the final counts.</summary>
[GenerateSerializer, Immutable]
public sealed record QuestionFinishedEventMessageComposer : IComposer
{
    [Id(0)]
    public required int QuestionId { get; init; }

    [Id(1)]
    public required IReadOnlyList<KeyValuePair<string, int>> AnswerCounts { get; init; }
}
