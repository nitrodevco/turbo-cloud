using System.Diagnostics.CodeAnalysis;
using Orleans;
using Turbo.Primitives.Navigator.Enums;
using Turbo.Primitives.Rooms.Enums;
using Turbo.Primitives.Rooms.Enums.Wired;
using Turbo.Primitives.Rooms.Snapshots.Settings;

namespace Turbo.Primitives.Rooms.Snapshots;

[GenerateSerializer, Immutable]
public sealed record RoomSnapshot : RoomInfoSnapshot
{
    public RoomSnapshot() { }

    /// <summary>
    /// Starts from a listing snapshot, so the fields the two share are mapped once. The attribute
    /// is what lets the base fields count as set; it also stops the compiler from checking the
    /// room-only fields below, so whoever calls this sets every one of them.
    /// </summary>
    [SetsRequiredMembers]
    public RoomSnapshot(RoomInfoSnapshot info)
        : base(info) { }

    [Id(0)]
    public required string Password { get; init; } = string.Empty;

    [Id(1)]
    public required ModSettingsSnapshot ModSettings { get; init; } = ModSettingsSnapshot.OwnerOnly;

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

    [Id(7)]
    public required bool LeaveOnDoorTile { get; init; } = false;

    [Id(8)]
    public required bool IdleSleepEnabled { get; init; } = false;

    [Id(9)]
    public required int IdleSleepTimeoutSeconds { get; init; } = 0;

    [Id(10)]
    public required bool IdleAutokickEnabled { get; init; } = false;

    [Id(11)]
    public required int IdleAutokickTimeoutSeconds { get; init; } = 0;

    [Id(12)]
    public required bool MuteAllPets { get; init; } = false;

    // 13 was HiddenByBc, which moved to RoomInfoSnapshot so the navigator could filter on it.
    // Orleans numbers ids per declaring type and a stored id is never reused, so the slot stays
    // empty rather than being filled by the next field added here.

    [Id(14)]
    public required WiredPermissionFlags WiredModifyPermissionMask { get; init; }

    [Id(15)]
    public required WiredPermissionFlags WiredReadPermissionMask { get; init; }

    [Id(16)]
    public required string WiredTimezone { get; init; } = string.Empty;

    /// <summary>
    /// The height every wall is drawn at, or -1 to let the client work it out from the plan. The
    /// floor plan editor's slider is what sets it, and draws the value plus one.
    /// </summary>
    [Id(17)]
    public int WallHeight { get; init; } = -1;
}
