using Orleans;
using Turbo.Primitives.Furniture.Snapshots;
using Turbo.Primitives.Furniture.Snapshots.StuffData;
using Turbo.Primitives.Rooms;

namespace Turbo.Primitives.Inventory.Snapshots;

[GenerateSerializer, Immutable]
public sealed record FurnitureItemSnapshot
{
    [Id(0)]
    public required int ItemId { get; init; }

    [Id(1)]
    public required int SpriteId { get; init; }

    [Id(2)]
    public required int OwnerId { get; init; }

    [Id(3)]
    public required FurnitureDefinitionSnapshot Definition { get; init; }

    [Id(4)]
    public required StuffDataSnapshot StuffData { get; init; }

    [Id(5)]
    public required string StuffDataJson { get; init; }

    [Id(6)]
    public required int SecondsToExpiration { get; init; }

    [Id(7)]
    public required bool HasRentPeriodStarted { get; init; }

    [Id(8)]
    public required RoomId RoomId { get; init; }

    [Id(9)]
    public string SlotId { get; init; } = string.Empty;

    [Id(10)]
    public int Extra { get; init; } = 0;
}
