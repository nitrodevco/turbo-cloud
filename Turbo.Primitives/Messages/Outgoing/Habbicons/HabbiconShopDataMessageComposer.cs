using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Habbicons.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Habbicons;

/// <summary>Every Habbicon collection with the player's states; sent at login and when asked.</summary>
[GenerateSerializer, Immutable]
public sealed record HabbiconShopDataMessageComposer : IComposer
{
    [Id(0)]
    public required ImmutableArray<HabbiconCollectionSnapshot> Collections { get; init; }
}
