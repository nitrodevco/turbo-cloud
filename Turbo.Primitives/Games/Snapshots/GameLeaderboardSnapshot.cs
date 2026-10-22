using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Games.Snapshots;

/// <summary>
/// A page of a game leaderboard, as the client's <c>Game2LeaderboardParser</c> and its weekly and
/// group forms read it. A group board ranks groups: an entry's id is the group's, its figure the
/// group's badge.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record GameLeaderboardSnapshot
{
    [Id(0)]
    public required ImmutableArray<GameLeaderboardEntrySnapshot> Entries { get; init; }

    /// <summary>Entries the whole board holds.</summary>
    [Id(1)]
    public required int TotalListSize { get; init; }

    [Id(2)]
    public required int GameTypeId { get; init; }

    [Id(3)]
    public int Year { get; init; }

    [Id(4)]
    public int Week { get; init; }

    /// <summary>How many weeks back the board can go.</summary>
    [Id(5)]
    public int MaxOffset { get; init; }

    /// <summary>The week shown, in weeks back from this one.</summary>
    [Id(6)]
    public int CurrentOffset { get; init; }

    [Id(7)]
    public int MinutesUntilReset { get; init; }

    [Id(8)]
    public int FavouriteGroupId { get; init; }
}

[GenerateSerializer, Immutable]
public sealed record GameLeaderboardEntrySnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int Score { get; init; }

    [Id(2)]
    public required int Rank { get; init; }

    [Id(3)]
    public required string Name { get; init; }

    [Id(4)]
    public required string Figure { get; init; }

    [Id(5)]
    public required string Gender { get; init; }
}
