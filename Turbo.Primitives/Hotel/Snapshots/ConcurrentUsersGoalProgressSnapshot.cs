using Orleans;
using Turbo.Primitives.Hotel.Enums;

namespace Turbo.Primitives.Hotel.Snapshots;

/// <summary>The concurrent users goal for one player: how many are online, of how many wanted.</summary>
[GenerateSerializer, Immutable]
public sealed record ConcurrentUsersGoalProgressSnapshot
{
    [Id(0)]
    public required ConcurrentUsersGoalState State { get; init; }

    [Id(1)]
    public required int UserCount { get; init; }

    /// <summary>The players wanted online at once; 0 when the goal is disabled.</summary>
    [Id(2)]
    public required int UserCountGoal { get; init; }
}
