using Orleans;

namespace Turbo.Primitives.Hotel.Snapshots;

/// <summary>One of a community goal's best contributors, as its hall of fame lists them.</summary>
[GenerateSerializer, Immutable]
public sealed record CommunityGoalContributorSnapshot
{
    [Id(0)]
    public required int PlayerId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Figure { get; init; }

    /// <summary>From 1.</summary>
    [Id(3)]
    public required int Rank { get; init; }

    [Id(4)]
    public required int Score { get; init; }
}
