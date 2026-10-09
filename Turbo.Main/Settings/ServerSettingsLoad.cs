using System.Collections.Generic;

namespace Turbo.Main.Settings;

/// <summary>
/// The admin panel's overrides as read from the database, each a path and a JSON value, and why
/// they couldn't be read, when they couldn't.
/// </summary>
internal sealed record ServerSettingsLoad(
    IReadOnlyList<(string Path, string Value)> Rows,
    string? Error
)
{
    public static readonly ServerSettingsLoad NONE = new([], null);
}
