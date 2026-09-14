using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

[GenerateSerializer, Immutable]
public sealed record WiredRoomStatsSnapshot
{
    [Id(0)]
    public required double ExecutionCost { get; init; }

    [Id(1)]
    public required double ExecutionCostCap { get; init; }

    [Id(2)]
    public required bool IsHeavy { get; init; }

    [Id(3)]
    public required int FloorItemCount { get; init; }

    [Id(4)]
    public required int FloorItemCap { get; init; }

    [Id(5)]
    public required int WallItemCount { get; init; }

    [Id(6)]
    public required int WallItemCap { get; init; }

    [Id(7)]
    public required int PermanentFurniVariables { get; init; }

    [Id(8)]
    public required int MaxPermanentFurniVariables { get; init; }

    [Id(9)]
    public required int PermanentUserVariables { get; init; }

    [Id(10)]
    public required int MaxPermanentUserVariables { get; init; }

    [Id(11)]
    public required int PermanentGlobalVariables { get; init; }

    [Id(12)]
    public required int MaxPermanentGlobalVariables { get; init; }
}
