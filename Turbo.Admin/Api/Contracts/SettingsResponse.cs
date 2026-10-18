using System.Collections.Immutable;
using Turbo.Primitives.Settings.Snapshots;

namespace Turbo.Admin.Api.Contracts;

/// <summary>
/// Every server setting, whether the signed-in staff member may change them, and why the panel's
/// overrides couldn't be read as the server started (<c>startupError</c>; null when they were).
/// </summary>
public sealed record SettingsResponse(
    ImmutableArray<ServerSettingSnapshot> Settings,
    bool CanManage,
    string? StartupError
);
