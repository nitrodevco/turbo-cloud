using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>A community goal's best contributors, best first.</summary>
[GenerateSerializer, Immutable]
public sealed record CommunityGoalHallOfFameMessageComposer : IComposer
{
    [Id(0)]
    public required string GoalCode { get; init; }

    [Id(1)]
    public required ImmutableArray<CommunityGoalContributorSnapshot> Contributors { get; init; }
}
