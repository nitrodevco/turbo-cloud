using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Catalog.Snapshots;
using Turbo.Primitives.Networking;

namespace Turbo.Primitives.Messages.Outgoing.Catalog;

[GenerateSerializer, Immutable]
public sealed record RoomAdPurchaseInfoEventMessageComposer : IComposer
{
    [Id(0)]
    public required bool IsVip { get; init; }

    [Id(1)]
    public required ImmutableArray<RoomAdPurchaseRoomSnapshot> Rooms { get; init; }
}
