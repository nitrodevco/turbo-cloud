using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomSnapshot : RoomInfoSnapshot
{
    [Id(0)]
    public required string Password { get; init; } = string.Empty;

    [Id(1)]
    public required ModSettingsSnapshot ModSettings { get; init; }

    [Id(2)]
    public required ChatFloodSensitivityType ChatProtection { get; init; }

    [Id(3)]
    public required string WorldType { get; init; } = string.Empty;

    [Id(4)]
    public required bool HideWalls { get; init; } = false;

    [Id(5)]
    public required RoomThicknessType WallThickness { get; init; } = RoomThicknessType.Normal;

    [Id(6)]
    public required RoomThicknessType FloorThickness { get; init; } = RoomThicknessType.Normal;
}
