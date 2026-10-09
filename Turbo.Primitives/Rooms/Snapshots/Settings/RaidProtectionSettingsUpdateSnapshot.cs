using Orleans;

namespace Turbo.Primitives.Rooms.Snapshots.Settings;

/// <summary>
/// A raid protection save as the client sends it. The values are untrusted ints: the room checks
/// each against what the client's menus offer before anything is written.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RaidProtectionSettingsUpdateSnapshot
{
    [Id(0)]
    public required bool Enabled { get; init; }

    [Id(1)]
    public required int DetectionSensitivity { get; init; }

    [Id(2)]
    public required int ActionType { get; init; }

    [Id(3)]
    public required int BanDurationSeconds { get; init; }

    [Id(4)]
    public required bool GuardEnabled { get; init; }

    [Id(5)]
    public required int GuardDurationSeconds { get; init; }

    [Id(6)]
    public required int GuardSensitivity { get; init; }

    /// <summary>Whether the player confirmed turning protection on.</summary>
    [Id(7)]
    public required bool Confirmed { get; init; }
}
