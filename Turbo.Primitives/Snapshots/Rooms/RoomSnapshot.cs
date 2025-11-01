using Orleans;
using Turbo.Contracts.Enums.Navigator;
using Turbo.Primitives.Snapshots.Navigator;

namespace Turbo.Primitives.Snapshots.Rooms;

[GenerateSerializer, Immutable]
public record RoomSnapshot
{
    [Id(0)]
    public required long Id { get; init; }

    [Id(1)]
    public required string Name { get; init; } = string.Empty;

    [Id(2)]
    public required string Description { get; init; } = string.Empty;

    [Id(3)]
    public required int OwnerId { get; init; }

    [Id(4)]
    public required DoorModeType DoorMode { get; init; }

    [Id(5)]
    public string? Password { get; init; }

    [Id(6)]
    public required int ModelId { get; init; }

    [Id(7)]
    public int? CategoryId { get; init; }

    [Id(8)]
    public required int PlayersMax { get; init; }

    [Id(9)]
    public required bool AllowPets { get; init; }

    [Id(10)]
    public required bool AllowPetsEat { get; init; }

    [Id(11)]
    public required TradeModeType TradeMode { get; init; }

    [Id(12)]
    public required ModSettingsSnapshot ModSettings { get; init; }

    [Id(13)]
    public required ChatSettingsSnapshot ChatSettings { get; init; }
}
