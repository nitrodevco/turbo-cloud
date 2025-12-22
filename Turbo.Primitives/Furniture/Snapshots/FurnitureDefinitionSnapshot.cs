using Orleans;
using Turbo.Primitives.Furniture.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Furniture.Snapshots;

[GenerateSerializer, Immutable]
public sealed record FurnitureDefinitionSnapshot
{
    [Id(0)]
    public required int Id { get; init; }

    [Id(1)]
    public required int SpriteId { get; init; }

    [Id(2)]
    public required string PublicName { get; init; }

    [Id(3)]
    public required ProductType ProductType { get; init; }

    [Id(4)]
    public required FurnitureCategory FurniCategory { get; init; }

    [Id(5)]
    public required string LogicName { get; init; }

    [Id(6)]
    public required int TotalStates { get; init; }

    [Id(7)]
    public required int Width { get; init; }

    [Id(8)]
    public required int Length { get; init; }

    [Id(9)]
    public required double StackHeight { get; init; }

    [Id(10)]
    public required bool CanStack { get; init; }

    [Id(11)]
    public required bool CanWalk { get; init; }

    [Id(12)]
    public required bool CanSit { get; init; }

    [Id(13)]
    public required bool CanLay { get; init; }

    [Id(14)]
    public required bool CanRecycle { get; init; }

    [Id(15)]
    public required bool CanTrade { get; init; }

    [Id(16)]
    public required bool CanGroup { get; init; }

    [Id(17)]
    public required bool CanSell { get; init; }

    [Id(18)]
    public required FurnitureUsageType UsagePolicy { get; init; }

    [Id(19)]
    public required string? ExtraData { get; init; }

    [Id(20)]
    public required bool IsWired { get; init; }

    [Id(21)]
    public required string WiredType { get; init; }
}
