using Orleans;
using Turbo.Primitives.Navigator.Enums;

namespace Turbo.Primitives.Rooms.Snapshots.Settings;

[GenerateSerializer, Immutable]
public sealed record ModSettingsSnapshot
{
    /// <summary>
    /// The most restrictive settings, and what a room falls back to: only the owner may moderate.
    /// A snapshot built from a row always carries the row's own settings; this is here so no
    /// caller can leave the field null.
    /// </summary>
    public static readonly ModSettingsSnapshot OwnerOnly = new()
    {
        WhoCanMute = ModSettingType.Owner,
        WhoCanKick = ModSettingType.Owner,
        WhoCanBan = ModSettingType.Owner,
    };

    [Id(0)]
    public required ModSettingType WhoCanMute { get; init; }

    [Id(1)]
    public required ModSettingType WhoCanKick { get; init; }

    [Id(2)]
    public required ModSettingType WhoCanBan { get; init; }
}
