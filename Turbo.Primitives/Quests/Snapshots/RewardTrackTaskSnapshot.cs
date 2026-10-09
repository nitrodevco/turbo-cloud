using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Quests.Snapshots;

/// <summary>A reward track task with the player's progress on it.</summary>
[GenerateSerializer, Immutable]
public sealed record RewardTrackTaskSnapshot
{
    /// <summary>Names the <c>reward_track.&lt;track&gt;.task.&lt;id&gt;.*</c> texts.</summary>
    [Id(0)]
    public required string Id { get; init; }

    [Id(1)]
    public required string ActionType { get; init; }

    [Id(2)]
    public required string Parameter { get; init; }

    [Id(3)]
    public required int ProgressCount { get; init; }

    /// <summary>Counts only for a player who owns the track's premium.</summary>
    [Id(4)]
    public required bool Premium { get; init; }

    [Id(5)]
    public required ImmutableArray<RewardTrackTaskLevelSnapshot> Levels { get; init; }
}
