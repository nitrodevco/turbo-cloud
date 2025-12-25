using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Furniture.Snapshots.WiredData;

[GenerateSerializer, Immutable]
public abstract record WiredDataSnapshot
{
    [Id(0)]
    public required int FurniLimit { get; init; }

    [Id(1)]
    public required List<int> StuffIds { get; init; }

    [Id(2)]
    public int StuffTypeId { get; init; }

    [Id(3)]
    public required int Id { get; init; }

    [Id(4)]
    public required string StringParam { get; init; }

    [Id(5)]
    public required List<int> IntParams { get; init; }

    [Id(6)]
    public required List<long> VariableIds { get; init; }

    [Id(7)]
    public required List<int> FurniSourceTypes { get; init; }

    [Id(8)]
    public required List<int> UserSourceTypes { get; init; }

    [Id(9)]
    public required int Code { get; init; }

    [Id(10)]
    public required bool AdvancedMode { get; init; }

    [Id(11)]
    public required List<(RoomObjectType, int)> AmountFurniSelections { get; init; }

    [Id(12)]
    public required bool AllowWallFurni { get; init; }
}
