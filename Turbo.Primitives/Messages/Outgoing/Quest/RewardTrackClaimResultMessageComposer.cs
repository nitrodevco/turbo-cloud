using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

[GenerateSerializer, Immutable]
public sealed record RewardTrackClaimResultMessageComposer : IComposer
{
    [Id(0)]
    public required string TrackId { get; init; }

    [Id(1)]
    public required string PrizeId { get; init; }

    [Id(2)]
    public required RewardTrackClaimResult Result { get; init; }
}
