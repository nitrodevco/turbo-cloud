using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// <see cref="IPluginStyles"/> over the hotel's own entries of <c>templates.styles</c> and
/// <c>asset.overlays</c>, written through the variables service as the panel's edits are.
/// </summary>
internal sealed class PluginStyleService(IGamedataVariableService variables) : IPluginStyles
{
    public async Task<ImmutableArray<PluginStyle>> ListAsync(
        string pluginKey,
        string folder,
        CancellationToken ct
    )
    {
        var properties = ThemeStyles.Read(folder);

        if (properties.IsEmpty)
            return [];

        var hotel = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        return [.. properties.Select(x => Of(pluginKey, x, hotel))];
    }

    public async Task<PluginStyle> SetAsync(
        string pluginKey,
        string folder,
        string name,
        string value,
        PlayerId actor,
        CancellationToken ct
    )
    {
        var property =
            Find(folder, name)
            ?? throw new ArgumentException($"The theme has no style {name}.", nameof(name));
        // A path in the plugin's wwwroot is the theme's own file; staff give urls (an upload's address).
        var client = ThemeStyles.ClientValue(property, value.Trim(), _ => null);

        var styles = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        styles[ThemeStyles.EntryOf(pluginKey, name)] = client;
        await SaveAsync(ExternalVariablesFile.TEMPLATE_STYLES, styles, actor, ct)
            .ConfigureAwait(false);

        if (property is { Type: ThemeStyleType.Image, Overlay: { } asset })
        {
            var overlays = await HotelAsync(ExternalVariablesFile.ASSET_OVERLAYS, ct)
                .ConfigureAwait(false);

            overlays[asset] = client.DeepClone();
            await SaveAsync(ExternalVariablesFile.ASSET_OVERLAYS, overlays, actor, ct)
                .ConfigureAwait(false);
        }

        return Of(pluginKey, property, styles);
    }

    public async Task<PluginStyle?> ResetAsync(
        string pluginKey,
        string folder,
        string name,
        PlayerId actor,
        CancellationToken ct
    )
    {
        if (Find(folder, name) is not { } property)
            return null;

        var styles = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        if (styles.Remove(ThemeStyles.EntryOf(pluginKey, name)))
            await SaveAsync(ExternalVariablesFile.TEMPLATE_STYLES, styles, actor, ct)
                .ConfigureAwait(false);

        if (property is { Type: ThemeStyleType.Image, Overlay: { } asset })
        {
            var overlays = await HotelAsync(ExternalVariablesFile.ASSET_OVERLAYS, ct)
                .ConfigureAwait(false);

            if (overlays.Remove(asset))
                await SaveAsync(ExternalVariablesFile.ASSET_OVERLAYS, overlays, actor, ct)
                    .ConfigureAwait(false);
        }

        return Of(pluginKey, property, styles);
    }

    private static ThemeStyleProperty? Find(string folder, string name) =>
        ThemeStyles.Read(folder).FirstOrDefault(x => x.Name == name);

    private static PluginStyle Of(
        string pluginKey,
        ThemeStyleProperty property,
        Dictionary<string, JsonNode?> hotel
    ) =>
        hotel.TryGetValue(ThemeStyles.EntryOf(pluginKey, property.Name), out var value)
            ? new PluginStyle(property, ThemeStyles.FromClient(property, value), true)
            : new PluginStyle(property, property.Default, false);

    /// <summary>The hotel's own entries of a merged variable: what staff set, never a plugin's.</summary>
    private async Task<Dictionary<string, JsonNode?>> HotelAsync(string key, CancellationToken ct)
    {
        var values = await variables.GetValuesAsync([key], ct).ConfigureAwait(false);

        return ThemeStyles.Entries(values.GetValueOrDefault(key));
    }

    /// <summary>The hotel's entries written back; none left takes the variable away.</summary>
    private async Task SaveAsync(
        string key,
        Dictionary<string, JsonNode?> entries,
        PlayerId actor,
        CancellationToken ct
    )
    {
        if (entries.Count == 0)
        {
            await variables.DeleteAsync(key, actor, ct).ConfigureAwait(false);

            return;
        }

        var value = new JsonObject();

        foreach (var (entry, node) in entries.OrderBy(x => x.Key, StringComparer.Ordinal))
            value[entry] = node?.DeepClone();

        await variables.SaveAsync(key, value.ToJsonString(), actor, ct).ConfigureAwait(false);
    }
}
