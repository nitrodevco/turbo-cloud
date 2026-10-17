using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record PurchaseOKMessageComposer : IComposer
{
    [Id(0)]
    public required CatalogOfferSnapshot Offer { get; init; }

    /// <summary>The floor furni the purchase created, listed after the offer.</summary>
    [Id(1)]
    public ImmutableArray<RoomObjectId> FloorItemIds { get; init; } = [];

    /// <summary>The wall furni the purchase created, listed after the floor furni.</summary>
    [Id(2)]
    public ImmutableArray<RoomObjectId> WallItemIds { get; init; } = [];
}
