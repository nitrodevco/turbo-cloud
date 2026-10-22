using System;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// The client gamedata a plugin brings in its folder: external variables in
/// <c>gamedata/external_variables.json</c> (a client config's JSON object, as the panel imports)
/// and external texts in <c>gamedata/external_texts.txt</c> (<c>key=value</c> lines, as Habbo's
/// file). As the plugin loads, the hotel is given each the hotel has none of, and the plugin owns
/// it from then on: while the hotel's value is still the one the plugin gave, it follows the
/// plugin's value when a new version gives another (or none), and it is sent to clients only while
/// the plugin is loaded (<see cref="Loaded"/>), so a plugin turned off, uninstalled with its data
/// kept, or failing to load never points clients at files it no longer serves. A key the hotel has
/// of its own, or changed since, is the hotel's: never touched, always sent. Uninstalling it with
/// its data takes away the ones still as the plugin gave them (the owner is an
/// <see cref="IPluginDataOwner"/> too).
/// </summary>
public interface IPluginGamedataSeeds
{
    /// <summary>
    /// Adds what the plugin in <paramref name="folder"/> brings that the hotel has none of, and
    /// gives the ones it owns its new values. A file that can't be read is logged and left out;
    /// the plugin loads either way.
    /// </summary>
    public Task SeedAsync(string pluginKey, string folder, CancellationToken ct);

    /// <summary>
    /// The plugin is loaded: its own variables and texts are sent to clients until the handle is
    /// disposed, as it unloads.
    /// </summary>
    public IDisposable Loaded(string pluginKey);
}
