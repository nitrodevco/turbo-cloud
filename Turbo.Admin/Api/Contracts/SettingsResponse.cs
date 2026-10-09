using System.Collections.Immutable;
using Turbo.Primitives.Settings.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>Every server setting, and whether the signed-in staff member may change them.</summary>
public sealed record SettingsResponse(
    ImmutableArray<ServerSettingSnapshot> Settings,
    bool CanManage
);
