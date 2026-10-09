using Orleans;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>A task's new count and the track's points.</summary>
[GenerateSerializer, Immutable]
public sealed record RewardTrackProgressMessageComposer : IComposer
{
    [Id(0)]
    public required string TrackId { get; init; }

    [Id(1)]
    public required string TaskId { get; init; }

    [Id(2)]
    public required int ProgressCount { get; init; }

    [Id(3)]
    public required int Points { get; init; }
}
