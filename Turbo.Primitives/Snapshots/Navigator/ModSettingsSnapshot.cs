using Turbo.Contracts.Enums.Navigator;

namespace Turbo.Primitives.Snapshots.Navigator;

public sealed record ModSettingsSnapshot(
    NavigatorModSettingType WhoCanMute,
    NavigatorModSettingType WhoCanKick,
    NavigatorModSettingType WhoCanBan
);
