using Orleans;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Players;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Object;

namespace Turbo.Primitives.Inventory.Snapshots;

[GenerateSerializer, Immutable]
public sealed record FurnitureItemSnapshot
{
    [Id(0)]
    public required RoomObjectId ItemId { get; init; }

    [Id(1)]
    public required int SpriteId { get; init; }

    [Id(2)]
    public required PlayerId OwnerId { get; init; }

    [Id(3)]
    public required string OwnerName { get; init; }

    [Id(4)]
    public required FurnitureDefinitionSnapshot Definition { get; init; }

    [Id(5)]
    public required StuffDataSnapshot StuffData { get; init; }

    [Id(6)]
    public required string ExtraData { get; init; }

    [Id(7)]
    public required int SecondsToExpiration { get; init; }

    [Id(8)]
    public required bool HasRentPeriodStarted { get; init; }

    [Id(9)]
    public required RoomId RoomId { get; init; }

    [Id(10)]
    public string SlotId { get; init; } = string.Empty;

    [Id(11)]
    public int Extra { get; init; } = 0;
}
