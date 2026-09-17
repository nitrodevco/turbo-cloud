using System.Collections.Immutable;
using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Settings;

/// <summary>
/// Room settings as the client sent them. Everything here is still untrusted: the room grain
/// checks permission, enum values, ranges and lengths before any of it is stored.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RoomSettingsUpdateSnapshot
{
    [Id(0)]
    public required string Name { get; init; }

    [Id(1)]
    public required string Description { get; init; }

    [Id(2)]
    public required int DoorMode { get; init; }

    [Id(3)]
    public required string Password { get; init; }

    [Id(4)]
    public required int MaximumVisitors { get; init; }

    /// <summary>A category the player may use, or null to leave the room uncategorised.</summary>
    [Id(5)]
    public required int? CategoryId { get; init; }

    [Id(6)]
    public required ImmutableArray<string> Tags { get; init; }

    [Id(7)]
    public required RoomTradeModeType TradeMode { get; init; }

    [Id(8)]
    public required bool AllowPets { get; init; }

    [Id(9)]
    public required bool AllowPetsEat { get; init; }

    [Id(10)]
    public required bool AllowWalkThrough { get; init; }

    [Id(11)]
    public required bool HideWalls { get; init; }

    [Id(12)]
    public required RoomThicknessType WallThickness { get; init; }

    [Id(13)]
    public required RoomThicknessType FloorThickness { get; init; }

    [Id(14)]
    public required ModSettingType WhoCanMute { get; init; }

    [Id(15)]
    public required ModSettingType WhoCanKick { get; init; }

    [Id(16)]
    public required ModSettingType WhoCanBan { get; init; }

    [Id(17)]
    public required ChatFloodSensitivityType ChatProtection { get; init; }

    [Id(18)]
    public required bool LeaveOnDoorTile { get; init; }

    [Id(19)]
    public required bool IdleSleepEnabled { get; init; }

    [Id(20)]
    public required int IdleSleepTimeoutSeconds { get; init; }

    [Id(21)]
    public required bool IdleAutokickEnabled { get; init; }

    [Id(22)]
    public required int IdleAutokickTimeoutSeconds { get; init; }

    [Id(23)]
    public required bool MuteAllPets { get; init; }
}
