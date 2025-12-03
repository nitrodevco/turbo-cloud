using Orleans;
using Turbo.Primitives.Furniture.Snapshots.StuffData;

namespace Turbo.Primitives.Inventory.Snapshots;

[GenerateSerializer, Immutable]
public sealed record FurnitureFloorItemSnapshot : FurnitureItemSnapshot
{
    [Id(0)]
    public required int ItemId { get; init; }

    [Id(1)]
    public required int SpriteId { get; init; }

    [Id(2)]
    public required StuffDataSnapshot StuffData { get; init; }

    [Id(3)]
    public required int SecondsToExpiration { get; init; }

    [Id(4)]
    public required bool HasRentPeriodStarted { get; init; }

    [Id(5)]
    public required int RoomId { get; init; }

    [Id(6)]
    public string SlotId { get; init; } = string.Empty;

    [Id(7)]
    public int Extra { get; init; } = 0;
}
