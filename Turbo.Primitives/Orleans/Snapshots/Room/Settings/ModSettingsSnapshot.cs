using Orleans;
using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Orleans.Snapshots.Room.Settings;

[GenerateSerializer, Immutable]
public sealed record ModSettingsSnapshot
{
    [Id(0)]
    public required ModSettingType WhoCanMute { get; init; }

    [Id(1)]
    public required ModSettingType WhoCanKick { get; init; }

    [Id(2)]
    public required ModSettingType WhoCanBan { get; init; }
}
