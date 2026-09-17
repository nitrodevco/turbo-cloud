using Orleans;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Catalog.Snapshots;

/// <summary>A room the player may promote, listed in the catalog's room ad page.</summary>
[GenerateSerializer, Immutable]
public sealed record RoomAdPurchaseRoomSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required bool HasControllers { get; init; }
}
