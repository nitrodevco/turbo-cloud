using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Snapshots.StuffData;

namespace Turbo.Primitives.Inventory.Snapshots;

[GenerateSerializer, Immutable]
public sealed record FurnitureFloorItemSnapshot : FurnitureItemSnapshot
{
    [Id(0)]
    public required int ItemId { get; init; }

    [Id(1)]
    public required int SpriteId { get; init; }

    [Id(2)]
    public required FurnitureType FurniCategory { get; init; }

    [Id(3)]
    public required ProductType ProductType { get; init; }

    [Id(4)]
    public required StuffDataSnapshot StuffData { get; init; }

    [Id(5)]
    public required bool CanRecycle { get; init; }

    [Id(6)]
    public required bool CanTrade { get; init; }

    [Id(7)]
    public required bool CanGroup { get; init; }

    [Id(8)]
    public required bool CanSell { get; init; }

    [Id(9)]
    public required int SecondsToExpiration { get; init; }

    [Id(10)]
    public required bool HasRentPeriodStarted { get; init; }

    [Id(11)]
    public required int RoomId { get; init; }
}
