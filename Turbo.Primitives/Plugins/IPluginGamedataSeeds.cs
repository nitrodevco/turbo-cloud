using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// The client gamedata a plugin brings in its folder: external variables in
/// <c>gamedata/external_variables.json</c> (a client config's JSON object, as the panel imports)
/// and external texts in <c>gamedata/external_texts.txt</c> (<c>key=value</c> lines, as Habbo's
/// file). As the plugin loads, the hotel is given each the hotel has none of; one the hotel has is
/// never touched. Uninstalling it with its data takes away the ones still as the plugin gave them
/// (the owner is an <see cref="IPluginDataOwner"/> too).
/// </summary>
public interface IPluginGamedataSeeds
{
    /// <summary>
    /// Adds what the plugin in <paramref name="folder"/> brings that the hotel has none of. A file
    /// that can't be read is logged and left out; the plugin loads either way.
    /// </summary>
    public Task SeedAsync(string pluginKey, string folder, CancellationToken ct);
}
