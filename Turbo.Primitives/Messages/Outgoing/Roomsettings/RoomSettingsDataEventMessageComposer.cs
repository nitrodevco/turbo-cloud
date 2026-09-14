using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Networking;
using Turbo.Primitives.Rooms;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Messages.Outgoing.Roomsettings;

[GenerateSerializer, Immutable]
public sealed record RoomSettingsDataEventMessageComposer : IComposer
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required string Name { get; init; }

    [Id(2)]
    public required string Description { get; init; }

    [Id(3)]
    public required RoomDoorModeType DoorMode { get; init; }

    [Id(4)]
    public required int CategoryId { get; init; }

    [Id(5)]
    public required int MaximumVisitors { get; init; }

    [Id(6)]
    public required int MaximumVisitorsLimit { get; init; }

    [Id(7)]
    public required ImmutableArray<string> Tags { get; init; }

    [Id(8)]
    public required RoomTradeModeType TradeMode { get; init; }

    [Id(9)]
    public required bool AllowPets { get; init; }

    [Id(10)]
    public required bool AllowFoodConsume { get; init; }

    [Id(11)]
    public required bool AllowWalkThrough { get; init; }

    [Id(12)]
    public required bool HideWalls { get; init; }

    [Id(13)]
    public required RoomThicknessType WallThickness { get; init; }

    [Id(14)]
    public required RoomThicknessType FloorThickness { get; init; }

    [Id(15)]
    public required ChatFloodSensitivityType ChatProtection { get; init; }

    [Id(16)]
    public required bool LeaveOnDoorTileEnabled { get; init; }

    [Id(17)]
    public required bool IdleSleepEnabled { get; init; }

    [Id(18)]
    public required int IdleSleepTimeoutSeconds { get; init; }

    [Id(19)]
    public required bool IdleAutokickEnabled { get; init; }

    [Id(20)]
    public required int IdleAutokickTimeoutSeconds { get; init; }

    [Id(21)]
    public required bool MuteAllPets { get; init; }

    [Id(22)]
    public required ModSettingsSnapshot ModSettings { get; init; }

    [Id(23)]
    public required bool HiddenByBc { get; init; }
}
