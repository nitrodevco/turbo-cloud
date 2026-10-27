using System.Collections.Immutable;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Turbo.Primitives.Plugins;

/// <summary>
/// A window patch staff made in the panel (nitro-next <c>docs/template-patches.md</c>): one template's
/// ops, with a name staff know it by; a disabled one is kept and not sent to clients.
/// </summary>
public sealed record HotelPatch(
    string Id,
    string Name,
    string Template,
    int? Order,
    JsonElement Ops,
    bool Disabled
);

/// <summary>
/// An image in a plugin's <c>wwwroot/styles/</c> (a theme's Theme tab, Images): <see cref="Path"/> is
/// where it is in the plugin's <c>wwwroot/</c>, as a patch's relative <c>asset</c> or an image style
/// names it; <see cref="Url"/> the address clients load it at, as a variable's value names it.
/// </summary>
public sealed record PluginImage(
    string Path,
    string? Url,
    long Bytes,
    System.DateTimeOffset Modified
);

/// <summary>
/// A font file in a plugin's <c>wwwroot/styles/</c> (a theme's Theme tab, Fonts): <see cref="Family"/>
/// is the name clients load it as (the file's name without its type), which a font style property
/// or a text style's <c>fontFamily</c> names; <see cref="Url"/> the address of the file, as the
/// <c>text.fonts</c> variable names it.
/// </summary>
public sealed record PluginFont(
    string Path,
    string? Url,
    string Family,
    long Bytes,
    System.DateTimeOffset Modified
);

/// <summary>
/// One entry of a plugin's <c>wwwroot/template-patches.json</c> (nitro-next
/// <c>docs/template-patches.md</c>): the template it patches, its ops as the file has them and its
/// order (null: the file's).
/// </summary>
public sealed record PluginTemplatePatch(string Template, JsonElement Ops, int? Order);

/// <summary>
/// A window a plugin defines as data (nitro-next <c>docs/template-patches.md</c>, "Windows as data"):
/// its <c>wwwroot/windows/&lt;name&gt;.json</c>, <see cref="Name"/> the file's name without its type,
/// <see cref="Title"/> what staff call it, <see cref="Url"/> the address clients load the file at
/// (null while the plugin is not loaded) and <see cref="File"/> the file as it is (null: not JSON).
/// </summary>
public sealed record PluginWindow(string Name, string? Title, string? Url, JsonElement? File);

/// <summary>A plugin made from the hotel's changes, as the panel names it.</summary>
public sealed record PluginExportRequest(string Key, string Name, string Version, string? Author);

/// <summary>
/// The no-code side of plugins (<c>docs/plugin-data.md</c>): the hotel's own data plugin
/// (<see cref="KEY"/>), which holds what staff make in the panel - window patches and the images
/// they upload - and is loaded as any plugin is; and a data plugin made from the hotel's changes,
/// a zip another hotel installs.
/// </summary>
public interface IHotelDataPlugin
{
    /// <summary>The key of the hotel's own data plugin.</summary>
    public const string KEY = "hotel";

    /// <summary>The folder of a plugin the server knows of; null for none.</summary>
    public string? FolderOf(string pluginKey);

    /// <summary>
    /// The patches staff made in the plugin <paramref name="pluginKey"/> (the hotel's own, or a data
    /// theme staff work on), disabled ones too, in the order they apply. A theme whose patches were
    /// written by hand (no <c>patches.json</c> yet) lists those of its <c>wwwroot/template-patches.json</c>,
    /// each named by its window. Throws <see cref="System.ArgumentException"/> for a plugin staff
    /// can't edit (<see cref="EditableFolderOf"/>).
    /// </summary>
    public Task<ImmutableArray<HotelPatch>> PatchesAsync(string pluginKey, CancellationToken ct);

    /// <summary>
    /// Adds the patch to the plugin <paramref name="pluginKey"/>, or replaces the one with its id, and
    /// loads that plugin again so clients are given it as they next load. Throws
    /// <see cref="System.ArgumentException"/> listing what is wrong with a patch that isn't one
    /// (<c>docs/template-patches.md</c>, Operations), or for a plugin staff can't edit.
    /// </summary>
    public Task<HotelPatch> SavePatchAsync(
        string pluginKey,
        HotelPatch patch,
        CancellationToken ct
    );

    /// <summary>Takes the patch away from the plugin; false when there is none with the id.</summary>
    public Task<bool> RemovePatchAsync(string pluginKey, string id, CancellationToken ct);

    /// <summary>
    /// The folder staff's edits to the plugin <paramref name="pluginKey"/> are written into (where they
    /// land, XenForo's development mode): the hotel's own plugin's, made when there is none, or a data
    /// plugin's (no code: a code plugin's files are its author's). Throws
    /// <see cref="System.ArgumentException"/> for a plugin with code and
    /// <see cref="System.Collections.Generic.KeyNotFoundException"/> for no plugin with the key.
    /// </summary>
    public string EditableFolderOf(string pluginKey);

    /// <summary>
    /// Loads the plugin again from its folder after staff changed its files, so clients are given
    /// them as they next load.
    /// </summary>
    public Task ReloadAsync(string pluginKey, CancellationToken ct);

    /// <summary>
    /// The window patches the plugin's <c>wwwroot/template-patches.json</c> gives clients, in the
    /// file's order (the hotel's own: its enabled ones); none without the file, null for no plugin
    /// with the key. Throws <see cref="System.ArgumentException"/> for a file that isn't JSON.
    /// </summary>
    public Task<ImmutableArray<PluginTemplatePatch>?> TemplatePatchesAsync(
        string pluginKey,
        CancellationToken ct
    );

    /// <summary>
    /// The windows the plugin defines as data, by name; none without a <c>wwwroot/windows/</c>, null
    /// for no plugin with the key. A file that isn't JSON is listed with a null <c>File</c>.
    /// </summary>
    public Task<ImmutableArray<PluginWindow>?> WindowsAsync(string pluginKey, CancellationToken ct);

    /// <summary>
    /// Writes the window <paramref name="name"/> into the plugin <paramref name="pluginKey"/>'s
    /// <c>wwwroot/windows/</c> (one by that name is replaced) and loads the plugin again, so clients
    /// are given it as they next load. Throws <see cref="System.ArgumentException"/> listing what is
    /// wrong with a file that isn't a window (<c>docs/template-patches.md</c>, "Windows as data"), a
    /// name that isn't one, or a plugin staff can't edit (<see cref="EditableFolderOf"/>).
    /// </summary>
    public Task<PluginWindow> SaveWindowAsync(
        string pluginKey,
        string name,
        JsonElement file,
        CancellationToken ct
    );

    /// <summary>Takes the window away from the plugin; false when it has none by the name.</summary>
    public Task<bool> RemoveWindowAsync(string pluginKey, string name, CancellationToken ct);

    /// <summary>
    /// Keeps a file staff uploaded in the hotel's plugin's <c>wwwroot/</c> at
    /// <paramref name="path"/>, and answers the address clients load it at. Throws
    /// <see cref="System.ArgumentException"/> for a path outside it or a type that isn't an image.
    /// </summary>
    public Task<string> SaveFileAsync(string path, Stream content, CancellationToken ct);

    /// <summary>
    /// The images in the plugin's <c>wwwroot/styles/</c>, by path; null for no plugin with the key.
    /// The hotel's own plugin has none until staff upload one.
    /// </summary>
    public Task<ImmutableArray<PluginImage>?> ImagesAsync(string pluginKey, CancellationToken ct);

    /// <summary>
    /// Keeps an image staff uploaded in the plugin's <c>wwwroot/styles/</c> as <paramref name="name"/>
    /// (one there by that name is replaced), so its patches and styles name it. Throws
    /// <see cref="System.ArgumentException"/> for a name that isn't a file name, or content that is no
    /// PNG, GIF, JPEG or WebP of the name's type; <see cref="System.Collections.Generic.KeyNotFoundException"/>
    /// for no plugin with the key.
    /// </summary>
    public Task<PluginImage> SaveImageAsync(
        string pluginKey,
        string name,
        Stream content,
        CancellationToken ct
    );

    /// <summary>Takes an image of the plugin's <c>wwwroot/styles/</c> away; false when there is none at <paramref name="path"/>.</summary>
    public Task<bool> RemoveImageAsync(string pluginKey, string path, CancellationToken ct);

    /// <summary>The font files in the plugin's <c>wwwroot/styles/</c>, by path; null for no plugin with the key.</summary>
    public Task<ImmutableArray<PluginFont>?> FontsAsync(string pluginKey, CancellationToken ct);

    /// <summary>
    /// Keeps a font file staff uploaded in the plugin's <c>wwwroot/styles/</c> as <paramref name="name"/>
    /// (one there by that name is replaced). A font is data: a browser loads it as a face, never runs
    /// it. Throws <see cref="System.ArgumentException"/> for a name that isn't a file name, or content
    /// that is no WOFF2, WOFF, TrueType or OpenType font of the name's type;
    /// <see cref="System.Collections.Generic.KeyNotFoundException"/> for no plugin with the key.
    /// </summary>
    public Task<PluginFont> SaveFontAsync(
        string pluginKey,
        string name,
        Stream content,
        CancellationToken ct
    );

    /// <summary>Takes a font file of the plugin's <c>wwwroot/styles/</c> away; false when there is none at <paramref name="path"/>.</summary>
    public Task<bool> RemoveFontAsync(string pluginKey, string path, CancellationToken ct);

    /// <summary>
    /// A data plugin's package, made from the plugin <paramref name="pluginKey"/> as this hotel has
    /// it: its files under the request's key, name and version, a theme's style properties defaulting
    /// to the hotel's values (and the images staff uploaded for them in it), the hotel's own plugin
    /// with only its enabled patches. Throws <see cref="System.ArgumentException"/> for a key the
    /// plugin folder can't take, or a plugin with code (its author's package is the one to share);
    /// null for no plugin with the key.
    /// </summary>
    public Task<byte[]?> ExportAsync(
        string pluginKey,
        PluginExportRequest request,
        CancellationToken ct
    );
}
