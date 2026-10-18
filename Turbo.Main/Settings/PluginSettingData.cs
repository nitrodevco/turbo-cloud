using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Main.Settings;

/// <summary>
/// A plugin's settings overridden in the panel (<see cref="IPluginDataOwner"/>): those of its
/// config classes, which the settings know of only while it is loaded, so the dry run lists them
/// before it is unloaded and the uninstall takes those paths away after.
/// </summary>
internal sealed class PluginSettingData(ServerSettingsService settings) : IPluginDataOwner
{
    public async Task<ImmutableArray<PluginDataItem>> FindAsync(
        PluginDataScope plugin,
        CancellationToken ct
    )
    {
        var own = settings
            .List()
            .Where(x => string.Equals(x.Plugin, plugin.Key, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(x => x.Path, StringComparer.OrdinalIgnoreCase);
        var overrides = await settings.OverridesAsync(own.Keys, ct).ConfigureAwait(false);

        return
        [
            .. overrides.Select(x => new PluginDataItem(
                PluginDataKind.Setting,
                x.Path,
                1,
                own[x.Path].Secret ? null : x.Value
            )),
        ];
    }

    public Task RemoveAsync(
        PluginDataScope plugin,
        ImmutableArray<PluginDataItem> found,
        PlayerId actor,
        CancellationToken ct
    ) =>
        settings.ForgetAsync(
            [
                .. found
                    .Where(x => x.Kind == PluginDataKind.Setting)
                    .Select(x => (x.Name, x.Detail is null)),
            ],
            actor,
            ct
        );
}
