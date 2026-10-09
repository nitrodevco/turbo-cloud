using Orleans;
using Turbo.Primitives.Hotel.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>Where the community goal stands, for the player asking.</summary>
[GenerateSerializer, Immutable]
public sealed record CommunityGoalProgressMessageComposer : IComposer
{
    [Id(0)]
    public required CommunityGoalProgressSnapshot Data { get; init; }
}
