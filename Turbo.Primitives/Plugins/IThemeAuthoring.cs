using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// A theme staff make in the panel (Appearance &gt; Themes, New theme): its key and name, and either
/// the theme it extends (<see cref="Extends"/>: the base theme <c>habbo</c>, the hotel's own, or any
/// installed theme; null: <c>habbo</c>) or the theme it is a copy of (<see cref="CopyOf"/>).
/// </summary>
public sealed record ThemeCreateRequest(
    string Key,
    string Name,
    string? Extends,
    string? CopyOf,
    string? Author
);

/// <summary>
/// Themes made in the panel (<c>docs/plugin-data.md</c>, "Themes made in the panel"): each is a data
/// plugin of its own in the plugin folder, made through the same plumbing a package installs by, that
/// staff's edits then land in while they work on it (<see cref="IHotelDataPlugin.EditableFolderOf"/>).
/// </summary>
public interface IThemeAuthoring
{
    /// <summary>
    /// Makes the theme and loads it: <c>plugins/&lt;key&gt;/</c> with a manifest naming no assembly,
    /// a <c>theme.json</c> that extends its parent, an empty <c>wwwroot/template-patches.json</c> and
    /// <c>wwwroot/styles/</c>; a copy has every file of the theme it copies as this hotel has it (its
    /// styles' values as its defaults, as Make a plugin does) under the new key, and its patches with
    /// their names. Throws <see cref="System.ArgumentException"/> for a key the plugin folder can't
    /// take or one in use, no name, or a parent or copy that is no installed theme.
    /// </summary>
    public Task<PluginTheme> CreateAsync(ThemeCreateRequest request, CancellationToken ct);
}
