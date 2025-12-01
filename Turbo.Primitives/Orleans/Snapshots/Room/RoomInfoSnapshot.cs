using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public record RoomInfoSnapshot : RoomSummarySnapshot
{
    [Id(0)]
    public required RoomDoorModeType DoorMode { get; init; } = RoomDoorModeType.Invisible;

    [Id(1)]
    public required int PlayersMax { get; init; } = 0;

    [Id(2)]
    public required RoomTradeModeType TradeType { get; init; } = RoomTradeModeType.Disabled;

    [Id(3)]
    public required int Score { get; init; } = 0;

    [Id(4)]
    public required int Ranking { get; init; } = 0;

    [Id(5)]
    public required int CategoryId { get; init; } = -1;

    [Id(6)]
    public required ImmutableArray<string> Tags { get; init; } = [];

    [Id(7)]
    public required bool AllowBlocking { get; init; } = false;

    [Id(8)]
    public required bool AllowPets { get; init; } = false;

    [Id(9)]
    public required bool AllowPetsEat { get; init; } = false;
}
