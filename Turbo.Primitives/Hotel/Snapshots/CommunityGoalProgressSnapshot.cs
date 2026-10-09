using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Hotel.Snapshots;

/// <summary>
/// Where the community goal stands for one player, as the client's <c>CommunityGoalData</c>
/// reads it.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record CommunityGoalProgressSnapshot
{
    [Id(0)]
    public required bool HasGoalExpired { get; init; }

    [Id(1)]
    public required int PersonalContributionScore { get; init; }

    /// <summary>The player's place among contributors, from 1; 0 for one who gave nothing.</summary>
    [Id(2)]
    public required int PersonalContributionRank { get; init; }

    [Id(3)]
    public required int CommunityTotalScore { get; init; }

    /// <summary>Levels reached: 0 to 3; in a versus goal -3 to 3, negative when side two leads.</summary>
    [Id(4)]
    public required int CommunityHighestAchievedLevel { get; init; }

    /// <summary>Score to the next level; in a versus goal negative while side two leads.</summary>
    [Id(5)]
    public required int ScoreRemainingUntilNextLevel { get; init; }

    [Id(6)]
    public required int PercentCompletionTowardsNextLevel { get; init; }

    [Id(7)]
    public required string GoalCode { get; init; }

    [Id(8)]
    public required int TimeRemainingInSeconds { get; init; }

    [Id(9)]
    public required ImmutableArray<int> RewardUserLimits { get; init; }
}
