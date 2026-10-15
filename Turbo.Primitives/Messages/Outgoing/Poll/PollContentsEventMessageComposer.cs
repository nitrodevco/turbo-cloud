using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Polls;

namespace Turbo.Primitives.Messages.Outgoing.Poll;

/// <summary>A started poll's questions (Flash parser poll/PollContentsParser.as:59).</summary>
[GenerateSerializer, Immutable]
public sealed record PollContentsEventMessageComposer : IComposer
{
    [Id(0)]
    public required int PollId { get; init; }

    [Id(1)]
    public required string StartMessage { get; init; }

    [Id(2)]
    public required string EndMessage { get; init; }

    [Id(3)]
    public required ImmutableArray<RoomPollQuestionSnapshot> Questions { get; init; }

    [Id(4)]
    public required bool NpsPoll { get; init; }
}
