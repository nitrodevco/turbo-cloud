using System.Collections.Generic;
using Orleans;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Wired.Variables;
using Turbo.Primitives.Rooms.Wired.Variable;

namespace Turbo.Primitives.Rooms.Snapshots.Wired;

[GenerateSerializer, Immutable]
public record WiredDataSnapshot
{
    [Id(0)]
    public required WiredType WiredType { get; init; }

    [Id(1)]
    public required int FurniLimit { get; init; }

    [Id(2)]
    public required List<int> StuffIds { get; init; }

    [Id(3)]
    public required List<int> StuffIds2 { get; init; }

    [Id(4)]
    public int StuffTypeId { get; init; }

    [Id(5)]
    public required int Id { get; init; }

    [Id(6)]
    public required string StringParam { get; init; }

    [Id(7)]
    public required List<int> IntParams { get; init; }

    [Id(8)]
    public required List<WiredVariableId> VariableIds { get; init; }

    [Id(9)]
    public required List<WiredFurniSourceType[]> FurniSourceTypes { get; init; }

    [Id(10)]
    public required List<WiredPlayerSourceType[]> PlayerSourceTypes { get; init; }

    [Id(11)]
    public required int Code { get; init; }

    [Id(12)]
    public required bool AdvancedMode { get; init; }

    [Id(13)]
    public required List<(RoomObjectType, int)> AmountFurniSelections { get; init; }

    [Id(14)]
    public required bool AllowWallFurni { get; init; }

    [Id(15)]
    public required List<WiredFurniSourceType[]> AllowedFurniSources { get; init; }

    [Id(16)]
    public required List<WiredPlayerSourceType[]> AllowedPlayerSources { get; init; }

    [Id(17)]
    public required List<WiredFurniSourceType[]> DefaultFurniSources { get; init; }

    [Id(18)]
    public required List<WiredPlayerSourceType[]> DefaultPlayerSources { get; init; }

    [Id(19)]
    public required List<object> DefinitionSpecifics { get; init; }

    [Id(20)]
    public required List<object> TypeSpecifics { get; init; }

    [Id(21)]
    public required List<WiredVariableContextSnapshot> ContextSnapshots { get; init; }

    [Id(22)]
    public required List<int> DefaultIntParams { get; init; }
}
