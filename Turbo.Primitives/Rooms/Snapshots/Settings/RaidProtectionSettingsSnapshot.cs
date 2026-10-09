using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Settings;

/// <summary>
/// One room's raid protection, as the client's <c>RaidProtectionSettingsSnapshot</c> reads it:
/// whether it is on, how readily it reacts and what it does, the door guard, and whether a raid
/// is happening now and when the last one was.
/// </summary>
[GenerateSerializer, Immutable]
public sealed record RaidProtectionSettingsSnapshot
{
    [Id(0)]
    public required RoomId RoomId { get; init; }

    [Id(1)]
    public required bool Enabled { get; init; }

    [Id(2)]
    public required RaidSensitivityType DetectionSensitivity { get; init; }

    [Id(3)]
    public required RaidActionType ActionType { get; init; }

    [Id(4)]
    public required int BanDurationSeconds { get; init; }

    [Id(5)]
    public required bool GuardEnabled { get; init; }

    [Id(6)]
    public required int GuardDurationSeconds { get; init; }

    [Id(7)]
    public required RaidSensitivityType GuardSensitivity { get; init; }

    [Id(8)]
    public required bool IncidentActive { get; init; }

    /// <summary>When the last raid was, in seconds since the epoch; 0 for never.</summary>
    [Id(9)]
    public required int LastRaidAtEpochSeconds { get; init; }
}
