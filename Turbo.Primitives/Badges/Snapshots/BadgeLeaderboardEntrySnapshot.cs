using Orleans;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Badges.Snapshots;

[GenerateSerializer, Immutable]
public sealed record BadgeLeaderboardEntrySnapshot
{
    [Id(0)]
    public required PlayerId PlayerId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Figure { get; init; }

    /// <summary>One-based; players with the same score share a rank.</summary>
    [Id(3)]
    public required int Rank { get; init; }

    [Id(4)]
    public required int Score { get; init; }
}
