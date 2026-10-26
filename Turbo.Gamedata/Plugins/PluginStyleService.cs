using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Gamedata.Variables;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Gamedata;
using Turbo.Primitives.Players;
using Turbo.Primitives.Plugins;

namespace Turbo.Gamedata.Plugins;

/// <summary>
/// <see cref="IPluginStyles"/> over the hotel's own entries of <c>templates.styles</c> and of the
/// variables a style also goes to (<see cref="ThemeStyles.TargetsOf"/>: <c>asset.overlays</c>,
/// <c>frame.overrides</c>, ...), written through the variables service as the panel's edits are. The
/// theme a style file extends is found among the server's plugins (<see cref="IHotelDataPlugin"/>).
/// </summary>
internal sealed partial class PluginStyleService(
    IGamedataVariableService variables,
    IServiceProvider? host = null
) : IPluginStyles
{
    /// <summary>The bitmaps one skin set replaces at most: the theme sheet has about 600.</summary>
    private const int MAX_OVERLAYS = 1000;

    private const int MAX_URL = 2048;

    public async Task<ImmutableArray<PluginStyle>> ListAsync(
        string pluginKey,
        string folder,
        CancellationToken ct
    )
    {
        var properties = Read(pluginKey, folder);

        if (properties.IsEmpty)
            return [];

        var hotel = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        return [.. properties.Select(x => Of(pluginKey, x, hotel))];
    }

    public PluginTheme? ThemeOf(string pluginKey, string? folder) =>
        ThemeStyles.ThemeOf(pluginKey, folder);

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
            Find(pluginKey, folder, name)
            ?? throw new ArgumentException($"The theme has no style {name}.", nameof(name));
        // A path is one of the plugin's served files (an image on its Theme tab), at its address by hash.
        var client = ThemeStyles.ClientValue(
            property,
            value.Trim(),
            path => host?.GetService<IClientModuleDirectory>()?.AddressOf(pluginKey, path)
        );

        var styles = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        styles[ThemeStyles.EntryOf(pluginKey, name)] = client;
        await SaveAsync(ExternalVariablesFile.TEMPLATE_STYLES, styles, actor, ct)
            .ConfigureAwait(false);

        foreach (var target in ThemeStyles.TargetsOf(property))
        {
            var entries = await HotelAsync(target[0], ct).ConfigureAwait(false);

            ThemeStyles.SetPath(entries, target, ThemeStyles.TargetValue(property, client));
            await SaveAsync(target[0], entries, actor, ct).ConfigureAwait(false);
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
        if (Find(pluginKey, folder, name) is not { } property)
            return null;

        var styles = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);

        if (styles.Remove(ThemeStyles.EntryOf(pluginKey, name)))
            await SaveAsync(ExternalVariablesFile.TEMPLATE_STYLES, styles, actor, ct)
                .ConfigureAwait(false);

        foreach (var target in ThemeStyles.TargetsOf(property))
        {
            var entries = await HotelAsync(target[0], ct).ConfigureAwait(false);

            if (ThemeStyles.RemovePath(entries, target))
                await SaveAsync(target[0], entries, actor, ct).ConfigureAwait(false);
        }

        return Of(pluginKey, property, styles);
    }

    public async Task<ImmutableDictionary<string, string>> SetOverlaysAsync(
        string folder,
        IReadOnlyDictionary<string, string?> overlays,
        PlayerId actor,
        CancellationToken ct
    )
    {
        var problems = new List<string>();

        if (overlays.Count == 0)
            problems.Add("Name at least one bitmap.");

        if (overlays.Count > MAX_OVERLAYS)
            problems.Add($"At most {MAX_OVERLAYS} bitmaps at once.");

        foreach (var (key, url) in overlays)
        {
            if (!OverlayKeyPattern().IsMatch(key))
                problems.Add($"{key} is no bitmap: <library>/<asset>.");
            else if (url is not null && (url.Length > MAX_URL || !ThemeStyles.IsUrl(url)))
                problems.Add($"{key}: {url} is no address (/a/path on the hotel, or https://...).");
        }

        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems), nameof(overlays));

        // The hotel theme's image styles by the bitmap each stands in for: those go through the style.
        var styled = Read(IHotelDataPlugin.KEY, folder)
            .Where(x => x is { Type: ThemeStyleType.Image, Overlay: not null })
            .GroupBy(x => x.Overlay!, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var styles = await HotelAsync(ExternalVariablesFile.TEMPLATE_STYLES, ct)
            .ConfigureAwait(false);
        var entries = await HotelAsync(ExternalVariablesFile.ASSET_OVERLAYS, ct)
            .ConfigureAwait(false);
        var stylesChanged = false;

        foreach (var (key, url) in overlays)
        {
            if (styled.TryGetValue(key, out var property))
            {
                var entry = ThemeStyles.EntryOf(IHotelDataPlugin.KEY, property.Name);

                if (url is null)
                    stylesChanged |= styles.Remove(entry);
                else
                {
                    styles[entry] = ThemeStyles.ClientValue(property, url, _ => null);
                    stylesChanged = true;
                }
            }

            if (url is null)
                entries.Remove(key);
            else
                entries[key] = JsonValue.Create(url);
        }

        if (stylesChanged)
            await SaveAsync(ExternalVariablesFile.TEMPLATE_STYLES, styles, actor, ct)
                .ConfigureAwait(false);

        await SaveAsync(ExternalVariablesFile.ASSET_OVERLAYS, entries, actor, ct)
            .ConfigureAwait(false);

        return entries
            .Where(x => x.Value is JsonValue)
            .ToImmutableDictionary(x => x.Key, x => x.Value!.ToString(), StringComparer.Ordinal);
    }

    public async Task SetFontAsync(string family, string? url, PlayerId actor, CancellationToken ct)
    {
        if (!FamilyPattern().IsMatch(family))
            throw new ArgumentException(
                $"\"{family}\" is no font family: letters, digits, '-', '_' and spaces.",
                nameof(family)
            );

        if (url is not null && (url.Length > MAX_URL || !ThemeStyles.IsUrl(url)))
            throw new ArgumentException(
                $"{url} is no address (/a/path on the hotel, or https://...).",
                nameof(url)
            );

        var fonts = await HotelAsync(ExternalVariablesFile.TEXT_FONTS, ct).ConfigureAwait(false);

        if (url is not null)
            fonts[family] = JsonValue.Create(url);
        else if (!fonts.Remove(family))
            return;

        await SaveAsync(ExternalVariablesFile.TEXT_FONTS, fonts, actor, ct).ConfigureAwait(false);
    }

    private ImmutableArray<ThemeStyleProperty> Read(string pluginKey, string folder) =>
        ThemeStyles.Read(
            pluginKey,
            folder,
            key => host?.GetService<IHotelDataPlugin>()?.FolderOf(key)
        );

    private ThemeStyleProperty? Find(string pluginKey, string folder, string name) =>
        Read(pluginKey, folder).FirstOrDefault(x => x.Name == name);

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

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]+/[A-Za-z0-9_.\-]+$")]
    private static partial Regex OverlayKeyPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_\-][A-Za-z0-9_\- ]{0,63}$")]
    private static partial Regex FamilyPattern();
}
