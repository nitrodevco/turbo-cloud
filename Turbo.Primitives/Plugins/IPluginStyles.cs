using System.Collections.Generic;
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

    /// <summary>Where a window first opens: staff type <c>x,y</c>, clients get <c>{ "x": 84, "y": 153 }</c>.</summary>
    Position,
}

/// <summary>
/// One style property a theme declares in its <c>theme.json</c>, or inherits from the theme it
/// extends: its name (what its patches read as <c>${style:name}</c>), type and default as staff
/// type it (null: the client's own, so nothing is sent until staff set it), how the panel groups
/// and labels it, and where else its value goes: for an image the library bitmap it stands in for
/// (<see cref="Overlay"/>, an <c>asset.overlays</c> entry), and <see cref="Variable"/>, a merged
/// variable and the path in it (<c>["frame.overrides", "catalog", "color"]</c>). <see cref="From"/>
/// is the theme whose default it has: the base theme <c>habbo</c>'s, or one that names it again.
/// </summary>
public sealed record ThemeStyleProperty(
    string Name,
    ThemeStyleType Type,
    string? Default,
    string? Group,
    string? Label,
    string? Hint,
    string? Overlay,
    double? Min,
    double? Max,
    ImmutableArray<string> Variable = default,
    string? From = null
);

/// <summary>
/// A plugin that is a theme: it has a <c>theme.json</c> (or is the hotel's own plugin, which extends
/// the base theme with none), and the theme it <c>"extends"</c>; null for one that extends none.
/// </summary>
public sealed record PluginTheme(string Key, string? Extends);

/// <summary>
/// A style property and the hotel's value of it: <see cref="Value"/> is the default while
/// <see cref="Changed"/> is false (null: the client's own).
/// </summary>
public sealed record PluginStyle(ThemeStyleProperty Property, string? Value, bool Changed);

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
    /// The plugin's style properties with the hotel's values: those of the theme its
    /// <c>theme.json</c> extends first (the hotel's own plugin, with no file, extends the base theme
    /// <c>habbo</c>), then its own, in the files' order; none without the file. Throws
    /// <see cref="System.ArgumentException"/> for a file that can't be read.
    /// </summary>
    public Task<ImmutableArray<PluginStyle>> ListAsync(
        string pluginKey,
        string folder,
        CancellationToken ct
    );

    /// <summary>
    /// The plugin as a theme, read from its <c>theme.json</c> in <paramref name="folder"/>; null for a
    /// plugin that is none. Throws <see cref="System.ArgumentException"/> for a file that isn't JSON.
    /// </summary>
    public PluginTheme? ThemeOf(string pluginKey, string? folder);

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

    /// <summary>
    /// Replaces many library bitmaps for the hotel at once (a skin set): each of
    /// <paramref name="overlays"/> is an <c>asset.overlays</c> key (<c>&lt;library&gt;/&lt;asset&gt;</c>)
    /// and the url of its image, or null to give the library's own back. A bitmap an image style of the
    /// hotel's theme stands in for (the base theme's window frames and purse) is set through that style,
    /// so its Theme tab shows it. Every entry is checked before anything is written: one that is wrong
    /// throws <see cref="System.ArgumentException"/> naming each, and nothing changes. Answers the
    /// hotel's <c>asset.overlays</c> entries after.
    /// </summary>
    public Task<ImmutableDictionary<string, string>> SetOverlaysAsync(
        string folder,
        IReadOnlyDictionary<string, string?> overlays,
        PlayerId actor,
        CancellationToken ct
    );

    /// <summary>
    /// Gives clients a font file as <paramref name="family"/> (the hotel's <c>text.fonts</c> entry), or
    /// takes it away (null <paramref name="url"/>). Throws <see cref="System.ArgumentException"/> for a
    /// family that is no name or a url that is no address.
    /// </summary>
    public Task SetFontAsync(string family, string? url, PlayerId actor, CancellationToken ct);

    /// <summary>Takes the hotel's value away, so the theme's default holds again; null for an unknown property.</summary>
    public Task<PluginStyle?> ResetAsync(
        string pluginKey,
        string folder,
        string name,
        PlayerId actor,
        CancellationToken ct
    );
}
