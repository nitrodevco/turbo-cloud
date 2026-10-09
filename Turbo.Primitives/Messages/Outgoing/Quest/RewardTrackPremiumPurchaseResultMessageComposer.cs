using Orleans;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Quests.Enums;

namespace Turbo.Primitives.Messages.Outgoing.Quest;

/// <summary>A premium purchase's result and the track's points after it.</summary>
[GenerateSerializer, Immutable]
public sealed record RewardTrackPremiumPurchaseResultMessageComposer : IComposer
{
    [Id(0)]
    public required string TrackId { get; init; }

    [Id(1)]
    public required RewardTrackPremiumResult Result { get; init; }

    [Id(2)]
    public required int Points { get; init; }
}
