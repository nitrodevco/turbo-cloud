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
    /// Gives the property the theme's own default in its <c>theme.json</c> in <paramref name="folder"/>
    /// (staff working on the theme: their edit lands in its files, XenForo's development mode), as
    /// staff type it; null takes the theme's own default away, so the one it inherits holds. An image
    /// uploaded into the theme is named by its path in its wwwroot. Throws
    /// <see cref="System.ArgumentException"/> for an unknown property or a value its type refuses. The
    /// plugin is to be loaded again for clients to be given it.
    /// </summary>
    public Task<PluginStyle> SetDefaultAsync(
        string pluginKey,
        string folder,
        string name,
        string? value,
        CancellationToken ct
    );

    /// <summary>The texts in a plugin's own files (its <c>gamedata/external_texts.txt</c>), by key; none without the file.</summary>
    public Task<ImmutableDictionary<string, string>> TextsAsync(
        string folder,
        CancellationToken ct
    );

    /// <summary>
    /// Sets texts in the theme <paramref name="pluginKey"/>'s own files (its
    /// <c>gamedata/external_texts.txt</c>): each of <paramref name="texts"/> a text key and what it
    /// says, or null to take the theme's text away. A theme's text is drawn for the players who get
    /// the theme, over the hotel's for every key staff never changed (<c>text.overrides</c>,
    /// turbo-cloud <c>docs/plugin-data.md</c>); the hotel's own texts are Gamedata &gt; Texts'. Every
    /// entry is checked before anything is written: one that is wrong throws
    /// <see cref="System.ArgumentException"/> naming each, and nothing changes. Answers the theme's
    /// texts after.
    /// </summary>
    public Task<ImmutableDictionary<string, string>> SetTextsAsync(
        string pluginKey,
        string folder,
        IReadOnlyDictionary<string, string?> texts,
        CancellationToken ct
    );

    /// <summary>
    /// Replaces many library bitmaps at once (a skin set) for the hotel, or in the theme
    /// <paramref name="pluginKey"/> staff work on: each of <paramref name="overlays"/> is an
    /// <c>asset.overlays</c> key (<c>&lt;library&gt;/&lt;asset&gt;</c>) and the url of its image, or
    /// null to give the library's own back. A bitmap an image style of the theme stands in for (the
    /// base theme's window frames and purse) is set through that style, so its Theme tab shows it.
    /// The hotel's go to its own entries of the variables; a theme's into its files (that style's
    /// default in its <c>theme.json</c>, the rest its <c>gamedata/external_variables.json</c>, its own
    /// images by their path), to be loaded again. Every entry is checked before anything is written:
    /// one that is wrong throws <see cref="System.ArgumentException"/> naming each, and nothing
    /// changes. Answers its <c>asset.overlays</c> entries after.
    /// </summary>
    public Task<ImmutableDictionary<string, string>> SetOverlaysAsync(
        string pluginKey,
        string folder,
        IReadOnlyDictionary<string, string?> overlays,
        PlayerId actor,
        CancellationToken ct
    );

    /// <summary>
    /// Gives clients a font file as <paramref name="family"/> (the hotel's <c>text.fonts</c> entry, or
    /// the theme <paramref name="pluginKey"/>'s in its <c>gamedata/external_variables.json</c>), or
    /// takes it away (null <paramref name="url"/>). Throws <see cref="System.ArgumentException"/> for a
    /// family that is no name or a url that is no address.
    /// </summary>
    public Task SetFontAsync(
        string pluginKey,
        string folder,
        string family,
        string? url,
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
