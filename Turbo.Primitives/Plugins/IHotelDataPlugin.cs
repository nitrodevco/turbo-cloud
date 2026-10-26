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
/// One entry of a plugin's <c>wwwroot/template-patches.json</c> (nitro-next
/// <c>docs/template-patches.md</c>): the template it patches, its ops as the file has them and its
/// order (null: the file's).
/// </summary>
public sealed record PluginTemplatePatch(string Template, JsonElement Ops, int? Order);

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

    /// <summary>The patches staff made, disabled ones too, in the order they apply.</summary>
    public Task<ImmutableArray<HotelPatch>> PatchesAsync(CancellationToken ct);

    /// <summary>
    /// Adds the patch, or replaces the one with its id, and loads the hotel's plugin again so clients
    /// are given it as they next load. Throws <see cref="System.ArgumentException"/> listing what is
    /// wrong with a patch that isn't one (<c>docs/template-patches.md</c>, Operations).
    /// </summary>
    public Task<HotelPatch> SavePatchAsync(HotelPatch patch, CancellationToken ct);

    /// <summary>Takes the patch away; false when there is none with the id.</summary>
    public Task<bool> RemovePatchAsync(string id, CancellationToken ct);

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
