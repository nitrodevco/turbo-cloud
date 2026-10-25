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
    /// Keeps a file staff uploaded in the hotel's plugin's <c>wwwroot/</c> at
    /// <paramref name="path"/>, and answers the address clients load it at. Throws
    /// <see cref="System.ArgumentException"/> for a path outside it or a type that isn't an image.
    /// </summary>
    public Task<string> SaveFileAsync(string path, Stream content, CancellationToken ct);

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
