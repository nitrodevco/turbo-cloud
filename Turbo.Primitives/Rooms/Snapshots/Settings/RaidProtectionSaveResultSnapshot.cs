using Orleans;
using Turbo.Primitives.Rooms.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Settings;

/// <summary>The answer to a raid protection save: how it went and the settings in force after it.</summary>
[GenerateSerializer, Immutable]
public sealed record RaidProtectionSaveResultSnapshot
{
    [Id(0)]
    public required RaidProtectionSaveResultType Result { get; init; }

    [Id(1)]
    public required RaidProtectionSettingsSnapshot Settings { get; init; }
}
