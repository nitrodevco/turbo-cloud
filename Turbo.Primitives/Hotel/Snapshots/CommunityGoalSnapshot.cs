using System;
using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Hotel.Enums;

namespace Turbo.Primitives.Hotel.Snapshots;

/// <summary>
/// A community goal: a campaign the whole hotel plays together from one time to another. Its
/// words are the external texts under its code (<c>landing.view.community.headline.&lt;code&gt;</c>).
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CommunityGoalSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    /// <summary>What the client looks its texts up by.</summary>
    [Id(1)]
    public required string Code { get; init; }

    [Id(2)]
    public required CommunityGoalMode Mode { get; init; }

    /// <summary>When it starts (UTC).</summary>
    [Id(3)]
    public required DateTime StartsAt { get; init; }

    /// <summary>When it ends (UTC); players see it as over after.</summary>
    [Id(4)]
    public required DateTime EndsAt { get; init; }

    /// <summary>
    /// The scores its levels are reached at, rising, three at most: the hotel's total, or in a
    /// versus goal how far one side is ahead.
    /// </summary>
    [Id(5)]
    public required ImmutableArray<int> LevelScores { get; init; }

    /// <summary>
    /// The last rank of each prize band, rising (<c>1, 10, 100</c>: the best, the next nine, the
    /// next ninety), as the prizes widget shows them.
    /// </summary>
    [Id(6)]
    public required ImmutableArray<int> RewardRanks { get; init; }

    /// <summary>The catalog page whose purchases count for the goal, or its first side.</summary>
    [Id(7)]
    public int? SideOnePageId { get; init; }

    /// <summary>The catalog page whose purchases count for a versus goal's second side.</summary>
    [Id(8)]
    public int? SideTwoPageId { get; init; }

    public bool IsVersus => Mode is CommunityGoalMode.Versus or CommunityGoalMode.VersusVote;
}
