using System.Collections.Immutable;
using Orleans;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Orleans.Snapshots.Room;

[GenerateSerializer, Immutable]
public record RoomInfoSnapshot
{
    [Id(0)]
    public required long RoomId { get; init; }

    [Id(1)]
    public required string RoomName { get; init; } = string.Empty;

    [Id(2)]
    public required long OwnerId { get; init; }

    [Id(3)]
    public required string OwnerName { get; init; } = string.Empty;

    [Id(4)]
    public required DoorModeType DoorMode { get; init; }

    [Id(5)]
    public required int UserCount { get; init; }

    [Id(6)]
    public required int PlayersMax { get; init; }

    [Id(7)]
    public required string Description { get; init; } = string.Empty;

    [Id(8)]
    public required TradeModeType TradeMode { get; init; }

    [Id(9)]
    public required int Score { get; init; }

    [Id(10)]
    public required int Ranking { get; init; }

    [Id(11)]
    public int? CategoryId { get; init; }

    [Id(12)]
    public required ImmutableArray<string> Tags { get; init; }

    [Id(13)]
    public required bool AllowPets { get; init; }

    [Id(14)]
    public required bool AllowPetsEat { get; init; }
}
