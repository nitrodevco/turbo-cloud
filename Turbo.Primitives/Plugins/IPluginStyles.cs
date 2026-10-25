using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Turbo.Primitives.Players;

namespace Turbo.Primitives.Plugins;

/// <summary>What a style property holds (<c>docs/plugin-data.md</c>).</summary>
public enum ThemeStyleType
{
    Color,
    Number,
    Image,
    Font,
    Text,
}

/// <summary>
/// One style property a theme declares in its <c>theme.json</c>: its name (what its patches read as
/// <c>${style:name}</c>), type and default as staff type it, how the panel groups and labels it,
/// and for an image the library bitmap it stands in for (<c>asset.overlays</c>), if any.
/// </summary>
public sealed record ThemeStyleProperty(
    string Name,
    ThemeStyleType Type,
    string Default,
    string? Group,
    string? Label,
    string? Hint,
    string? Overlay,
    double? Min,
    double? Max
);

/// <summary>A style property and the hotel's value of it: <see cref="Value"/> is the default while <see cref="Changed"/> is false.</summary>
public sealed record PluginStyle(ThemeStyleProperty Property, string Value, bool Changed);

/// <summary>
/// The hotel's values of a theme's style properties: staff's changes over the defaults its
/// <c>theme.json</c> declares, kept as the hotel's own entries of the merged variable
/// <c>templates.styles</c> (and of <c>asset.overlays</c> for an image standing in for a bitmap),
/// so each is a change set of the variables' history and stays across the theme's updates. Clients
/// read them as they load.
/// </summary>
public interface IPluginStyles
{
    /// <summary>
    /// The plugin's style properties with the hotel's values, in <c>theme.json</c>'s order; none
    /// without the file. Throws <see cref="System.ArgumentException"/> for a file that can't be read.
    /// </summary>
    public Task<ImmutableArray<PluginStyle>> ListAsync(
        string pluginKey,
        string folder,
        CancellationToken ct
    );

    /// <summary>
    /// Gives the property the hotel's own value, as staff type it (a colour <c>#rrggbb</c>, an image
    /// a url). Throws <see cref="System.ArgumentException"/> for an unknown property or a value its
    /// type refuses.
    /// </summary>
    public Task<PluginStyle> SetAsync(
        string pluginKey,
        string folder,
        string name,
        string value,
        PlayerId actor,
        CancellationToken ct
    );

    /// <summary>Takes the hotel's value away, so the theme's default holds again; null for an unknown property.</summary>
    public Task<PluginStyle?> ResetAsync(
        string pluginKey,
        string folder,
        string name,
        PlayerId actor,
        CancellationToken ct
    );
}
