using System.Collections.Generic;
using Orleans;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Orleans.Snapshots.Room.Settings;

[GenerateSerializer, Immutable]
public sealed record RoomSettingsSnapshot
{
    [Id(0)]
    public required long Id { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required int OwnerId { get; init; }

    [Id(3)]
    public required string OwnerName { get; init; }

    [Id(4)]
    public required DoorModeType DoorMode { get; init; }

    [Id(5)]
    public required int UserCount { get; init; }

    [Id(6)]
    public required int MaxUserCount { get; init; }

    [Id(7)]
    public required string Description { get; init; }

    [Id(8)]
    public required TradeModeType TradeMode { get; init; }

    [Id(9)]
    public required int Score { get; init; }

    [Id(10)]
    public required int Ranking { get; init; }

    [Id(11)]
    public required int CategoryId { get; init; }

    [Id(12)]
    public required List<string> Tags { get; init; }

    [Id(13)]
    public required RoomBitmask Bitmask { get; init; }
}
