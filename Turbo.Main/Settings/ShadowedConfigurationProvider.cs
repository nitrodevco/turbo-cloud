using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Turbo.Main.Settings;

/// <summary>
/// A layer below the admin panel's overrides (<c>appsettings.json</c>, say), as it is, except for
/// what it gives under a list or map the panel overrides: configuration merges keys across
/// layers, so a list of two over a list of five would keep the last three. Hidden here, the
/// panel's list is the whole list.
/// </summary>
internal sealed class ShadowedConfigurationProvider(
    IConfigurationProvider inner,
    ServerSettingsOverrides overrides
) : IConfigurationProvider
{
    public IConfigurationProvider Inner { get; } = inner;

    public bool TryGet(string key, out string? value)
    {
        if (overrides.Hides(key))
        {
            value = null;

            return false;
        }

        return Inner.TryGet(key, out value);
    }

    public void Set(string key, string? value) => Inner.Set(key, value);

    public IChangeToken GetReloadToken() => Inner.GetReloadToken();

    public void Load() => Inner.Load();

    public IEnumerable<string> GetChildKeys(IEnumerable<string> earlierKeys, string? parentPath) =>
        earlierKeys
            .Concat(
                Inner
                    .GetChildKeys([], parentPath)
                    .Where(x =>
                        !overrides.Hides(
                            parentPath is null ? x : $"{parentPath}{SettingJson.KEY_DELIMITER}{x}"
                        )
                    )
            )
            .OrderBy(x => x, ConfigurationKeyComparer.Instance);

    public override string? ToString() => Inner.ToString();

    /// <summary>Builds the layer it wraps, shadowed.</summary>
    public sealed class Source(IConfigurationSource inner, ServerSettingsOverrides overrides)
        : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) =>
            new ShadowedConfigurationProvider(inner.Build(builder), overrides);
    }
}
