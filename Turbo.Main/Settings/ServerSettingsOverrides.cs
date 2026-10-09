using System;
using System.Collections.Generic;

namespace Turbo.Main.Settings;

/// <summary>
/// The lists and maps the admin panel overrides whole, shared between its provider, which sets
/// them as it reads the overrides, and the layers below it (<see cref="ShadowedConfigurationProvider"/>),
/// which hide what they give under each.
/// </summary>
internal sealed class ServerSettingsOverrides
{
    private volatile HashSet<string> _whole = new(StringComparer.OrdinalIgnoreCase);

    public void Replace(IEnumerable<string> paths) =>
        _whole = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);

    /// <summary>Whether the key is an overridden list or map, or inside one.</summary>
    public bool Hides(string key)
    {
        var whole = _whole;

        if (whole.Count == 0)
            return false;

        if (whole.Contains(key))
            return true;

        // Each parent of the key: Turbo:Admin:CatalogLayouts:3 is in Turbo:Admin:CatalogLayouts.
        for (
            var end = key.LastIndexOf(SettingJson.KEY_DELIMITER);
            end > 0;
            end = key.LastIndexOf(SettingJson.KEY_DELIMITER, end - 1)
        )
            if (whole.Contains(key[..end]))
                return true;

        return false;
    }
}
