using System.Collections.Immutable;
using Orleans;

namespace Turbo.Primitives.Hotel.Snapshots;

/// <summary>A community goal's score so far, as staff see it: each side's, and who gave most.</summary>
[GenerateSerializer, Immutable]
public sealed record CommunityGoalStandingSnapshot
{
    [Id(0)]
    public required int GoalId { get; init; }

    /// <summary>The goal's total, or in a versus goal side one's.</summary>
    [Id(1)]
    public required int SideOne { get; init; }

    /// <summary>A versus goal's side two; 0 for a goal of one side.</summary>
    [Id(2)]
    public required int SideTwo { get; init; }

    [Id(3)]
    public required int Contributors { get; init; }

    /// <summary>Players who voted for side one and for side two.</summary>
    [Id(4)]
    public required int VotesOne { get; init; }

    [Id(5)]
    public required int VotesTwo { get; init; }

    [Id(6)]
    public required ImmutableArray<CommunityGoalContributorSnapshot> Top { get; init; }
}
