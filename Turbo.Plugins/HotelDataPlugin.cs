using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.ClientModules;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// <see cref="IHotelDataPlugin"/> over the plugin folder: the hotel's plugin is the folder
/// <see cref="IHotelDataPlugin.KEY"/> there, a manifest with no assembly, made the first time staff
/// save something. Staff's patches are kept in its <c>patches.json</c> (with their names and
/// whether they are disabled) and written out for clients as its
/// <c>wwwroot/template-patches.json</c>, enabled ones only.
/// </summary>
internal sealed partial class HotelDataPlugin(PluginManager manager, IServiceProvider host)
    : IHotelDataPlugin
{
    private const string PATCHES = "patches.json";
    private const string MANIFEST = "manifest.json";
    private const string WWWROOT = PluginClientModules.WWWROOT;
    private const string TEMPLATE_PATCHES = "template-patches.json";
    private const string THEME = "theme.json";
    private const string BASE_THEME = "habbo";
    private const string STYLES = "styles";

    private static readonly string[] IMAGES = [".png", ".gif", ".jpg", ".jpeg", ".webp"];

    private static readonly string[] FONTS = [".woff2", ".woff", ".ttf", ".otf"];

    private static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>Held while the hotel's plugin's files change: one change at a time.</summary>
    private readonly SemaphoreSlim _changes = new(1, 1);

    private string Folder => Path.Combine(manager.Config.PluginFolderPath, IHotelDataPlugin.KEY);

    /// <summary>
    /// The hotel's plugin's manifest as this server takes it. The server writes that file itself,
    /// with the plugin API it has (<see cref="Ensure"/>), so one written under another API is
    /// stamped with this server's before the API check; any other folder's manifest is
    /// <paramref name="manifest"/> as it is.
    /// </summary>
    internal static PluginManifest Own(string folder, PluginManifest manifest)
    {
        if (
            !string.Equals(Path.GetFileName(folder), IHotelDataPlugin.KEY, StringComparison.Ordinal)
            || !string.Equals(manifest.Key, IHotelDataPlugin.KEY, StringComparison.Ordinal)
            || !PluginHelpers.IsDataOnly(manifest)
            || manifest.ApiVersion is not { } api
            || api == PluginApi.VERSION
        )
            return manifest;

        var path = Path.Combine(folder, MANIFEST);
        var json = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var name =
            json.Select(x => x.Key)
                .FirstOrDefault(x =>
                    string.Equals(
                        x,
                        nameof(PluginManifest.ApiVersion),
                        StringComparison.OrdinalIgnoreCase
                    )
                )
            ?? nameof(PluginManifest.ApiVersion);

        json[name] = PluginApi.VERSION;
        File.WriteAllText(
            path,
            json.ToJsonString(new JsonSerializerOptions { WriteIndented = true })
        );

        return PluginHelpers.ReadManifest(folder);
    }

    // The hotel's own plugin has its folder before it is made: its Theme tab and images are there from the start.
    public string? FolderOf(string pluginKey) =>
        manager.FoundFolderOf(pluginKey) ?? (pluginKey == IHotelDataPlugin.KEY ? Folder : null);

    public async Task<ImmutableArray<HotelPatch>> PatchesAsync(CancellationToken ct) =>
        [.. await ReadAsync(ct).ConfigureAwait(false)];

    public async Task<HotelPatch> SavePatchAsync(HotelPatch patch, CancellationToken ct)
    {
        var problems = TemplatePatchSchema.Problems(patch.Template.Trim(), patch.Ops);

        if (string.IsNullOrWhiteSpace(patch.Name))
            problems.Insert(0, "The patch has a name staff know it by.");

        if (problems.Count > 0)
            throw new ArgumentException(string.Join(" ", problems), nameof(patch));

        var saved = patch with
        {
            Id = string.IsNullOrWhiteSpace(patch.Id)
                ? Guid.NewGuid().ToString("N")[..12]
                : patch.Id,
            Name = patch.Name.Trim(),
            Template = patch.Template.Trim(),
            Ops = patch.Ops.Clone(),
        };

        await ChangeAsync(
                list =>
                {
                    var at = list.FindIndex(x => x.Id == saved.Id);

                    if (at < 0)
                        list.Add(saved);
                    else
                        list[at] = saved;

                    return true;
                },
                ct
            )
            .ConfigureAwait(false);

        return saved;
    }

    public Task<bool> RemovePatchAsync(string id, CancellationToken ct) =>
        ChangeAsync(list => list.RemoveAll(x => x.Id == id) > 0, ct);

    public async Task<string> SaveFileAsync(string path, Stream content, CancellationToken ct)
    {
        var relative = path.Replace('\\', '/').Trim('/');

        if (
            !FilePattern().IsMatch(relative)
            || relative.Split('/').Any(x => x is "." or ".." || x.StartsWith('.'))
        )
            throw new ArgumentException(
                $"\"{path}\" is a path of letters, digits, '-', '_' and '/'.",
                nameof(path)
            );

        if (!IMAGES.Contains(Path.GetExtension(relative).ToLowerInvariant()))
            throw new ArgumentException($"An image is {string.Join(", ", IMAGES)}.", nameof(path));

        await _changes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var created = Ensure();
            var file = Path.Combine(Folder, WWWROOT, relative);

            Directory.CreateDirectory(Path.GetDirectoryName(file)!);

            var stream = File.Create(file);
            await using (stream.ConfigureAwait(false))
                await content.CopyToAsync(stream, ct).ConfigureAwait(false);

            // Made just now, it is not served yet.
            if (created || manager.FolderOf(IHotelDataPlugin.KEY) is null)
                await manager.ReloadAsync(IHotelDataPlugin.KEY, ct).ConfigureAwait(false);
        }
        finally
        {
            _changes.Release();
        }

        return host.GetService<IClientModuleDirectory>()?.AddressOf(IHotelDataPlugin.KEY, relative)
            ?? $"/gamedata/plugins/{IHotelDataPlugin.KEY}/{relative}";
    }

    public Task<ImmutableArray<PluginImage>?> ImagesAsync(string pluginKey, CancellationToken ct)
    {
        if (FolderOf(pluginKey) is not { } folder)
            return Task.FromResult<ImmutableArray<PluginImage>?>(null);

        var styles = Path.Combine(folder, WWWROOT, STYLES);

        if (!Directory.Exists(styles))
            return Task.FromResult<ImmutableArray<PluginImage>?>([]);

        var images = Directory
            .EnumerateFiles(styles, "*", SearchOption.AllDirectories)
            .Select(x => new FileInfo(x))
            .Where(x => IMAGES.Contains(x.Extension.ToLowerInvariant()))
            .Select(x => Image(pluginKey, folder, x))
            .Where(x => !x.Path.Split('/').Any(part => part.StartsWith('.')))
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        return Task.FromResult<ImmutableArray<PluginImage>?>(images);
    }

    public Task<ImmutableArray<PluginFont>?> FontsAsync(string pluginKey, CancellationToken ct)
    {
        if (FolderOf(pluginKey) is not { } folder)
            return Task.FromResult<ImmutableArray<PluginFont>?>(null);

        var styles = Path.Combine(folder, WWWROOT, STYLES);

        if (!Directory.Exists(styles))
            return Task.FromResult<ImmutableArray<PluginFont>?>([]);

        var fonts = Directory
            .EnumerateFiles(styles, "*", SearchOption.AllDirectories)
            .Select(x => new FileInfo(x))
            .Where(x => FONTS.Contains(x.Extension.ToLowerInvariant()))
            .Select(x => Font(pluginKey, folder, x))
            .Where(x => !x.Path.Split('/').Any(part => part.StartsWith('.')))
            .OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToImmutableArray();

        return Task.FromResult<ImmutableArray<PluginFont>?>(fonts);
    }

    public async Task<ImmutableArray<PluginTemplatePatch>?> TemplatePatchesAsync(
        string pluginKey,
        CancellationToken ct
    )
    {
        if (FolderOf(pluginKey) is not { } folder)
            return null;

        var path = Path.Combine(folder, WWWROOT, TEMPLATE_PATCHES);

        if (!File.Exists(path))
            return [];

        JsonDocument doc;

        try
        {
            doc = JsonDocument.Parse(
                await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false),
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }
            );
        }
        catch (JsonException ex)
        {
            throw new ArgumentException(
                $"{TEMPLATE_PATCHES} isn't JSON. {ex.Message}",
                nameof(pluginKey),
                ex
            );
        }

        using (doc)
        {
            if (
                doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("patches", out var patches)
                || patches.ValueKind != JsonValueKind.Array
            )
                return [];

            // What isn't a patch is the client's to refuse and report; it is left out here.
            return patches
                .EnumerateArray()
                .Where(x =>
                    x.ValueKind == JsonValueKind.Object
                    && x.TryGetProperty("template", out var template)
                    && template.ValueKind == JsonValueKind.String
                )
                .Select(x => new PluginTemplatePatch(
                    x.GetProperty("template").GetString()!,
                    x.TryGetProperty("ops", out var ops)
                        ? ops.Clone()
                        : JsonSerializer.SerializeToElement(Array.Empty<object>()),
                    x.TryGetProperty("order", out var order) && order.TryGetInt32(out var value)
                        ? value
                        : null
                ))
                .ToImmutableArray();
        }
    }

    public async Task<PluginImage> SaveImageAsync(
        string pluginKey,
        string name,
        Stream content,
        CancellationToken ct
    )
    {
        var (folder, file) = await SaveStyleFileAsync(
                pluginKey,
                name,
                content,
                IMAGES,
                "An image",
                ct
            )
            .ConfigureAwait(false);

        return Image(pluginKey, folder, file);
    }

    public async Task<PluginFont> SaveFontAsync(
        string pluginKey,
        string name,
        Stream content,
        CancellationToken ct
    )
    {
        var (folder, file) = await SaveStyleFileAsync(pluginKey, name, content, FONTS, "A font", ct)
            .ConfigureAwait(false);

        return Font(pluginKey, folder, file);
    }

    /// <summary>
    /// Keeps a file staff uploaded in the plugin's <c>wwwroot/styles/</c> as <paramref name="name"/>
    /// (one there by that name is replaced), once its content is checked to be of the name's type
    /// (<see cref="IsStyleFile"/>): one of <paramref name="types"/>, which <paramref name="kind"/>
    /// names in a refusal.
    /// </summary>
    private async Task<(string Folder, FileInfo File)> SaveStyleFileAsync(
        string pluginKey,
        string name,
        Stream content,
        string[] types,
        string kind,
        CancellationToken ct
    )
    {
        var file = Path.GetFileName(name.Replace('\\', '/').Trim());
        var extension = Path.GetExtension(file).ToLowerInvariant();

        if (!ImageNamePattern().IsMatch(file))
            throw new ArgumentException(
                $"\"{name}\" is a file name of letters, digits, '-', '_' and '.'.",
                nameof(name)
            );

        if (!types.Contains(extension))
            throw new ArgumentException($"{kind} is {string.Join(", ", types)}.", nameof(name));

        using var memory = new MemoryStream();
        await content.CopyToAsync(memory, ct).ConfigureAwait(false);

        if (!IsStyleFile(memory.GetBuffer().AsSpan(0, (int)memory.Length), extension))
            throw new ArgumentException(
                $"{file} is not a {extension.TrimStart('.').ToUpperInvariant()} file.",
                nameof(content)
            );

        await _changes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var hotel = pluginKey == IHotelDataPlugin.KEY;
            var created = hotel && Ensure();
            var folder =
                FolderOf(pluginKey)
                ?? throw new KeyNotFoundException("There is no plugin with that key.");
            var path = Path.Combine(folder, WWWROOT, STYLES, file);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllBytesAsync(path, memory.ToArray(), ct).ConfigureAwait(false);

            // Made just now, it is not served yet.
            if (created || (hotel && manager.FolderOf(IHotelDataPlugin.KEY) is null))
                await manager.ReloadAsync(IHotelDataPlugin.KEY, ct).ConfigureAwait(false);

            return (folder, new FileInfo(path));
        }
        finally
        {
            _changes.Release();
        }
    }

    public Task<bool> RemoveImageAsync(string pluginKey, string path, CancellationToken ct) =>
        RemoveStyleFileAsync(pluginKey, path, IMAGES, ct);

    public Task<bool> RemoveFontAsync(string pluginKey, string path, CancellationToken ct) =>
        RemoveStyleFileAsync(pluginKey, path, FONTS, ct);

    private async Task<bool> RemoveStyleFileAsync(
        string pluginKey,
        string path,
        string[] types,
        CancellationToken ct
    )
    {
        if (FolderOf(pluginKey) is not { } folder)
            return false;

        var root = Path.GetFullPath(Path.Combine(folder, WWWROOT, STYLES));
        var full = Path.GetFullPath(Path.Combine(folder, WWWROOT, path.Replace('\\', '/')));

        if (
            !full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !types.Contains(Path.GetExtension(full).ToLowerInvariant())
            || !File.Exists(full)
        )
            return false;

        await _changes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            File.Delete(full);
        }
        finally
        {
            _changes.Release();
        }

        return true;
    }

    private PluginImage Image(string pluginKey, string folder, FileInfo file)
    {
        var path = Path.GetRelativePath(Path.Combine(folder, WWWROOT), file.FullName)
            .Replace('\\', '/');

        return new PluginImage(
            path,
            host.GetService<IClientModuleDirectory>()?.AddressOf(pluginKey, path),
            file.Length,
            new DateTimeOffset(file.LastWriteTimeUtc, TimeSpan.Zero)
        );
    }

    private PluginFont Font(string pluginKey, string folder, FileInfo file)
    {
        var image = Image(pluginKey, folder, file);

        return new PluginFont(
            image.Path,
            image.Url,
            Path.GetFileNameWithoutExtension(file.Name),
            image.Bytes,
            image.Modified
        );
    }

    /// <summary>
    /// Whether <paramref name="bytes"/> start as a file of <paramref name="extension"/>'s type does:
    /// an image, or a font (an <c>.otf</c> may hold TrueType outlines, as OpenType allows).
    /// </summary>
    private static bool IsStyleFile(ReadOnlySpan<byte> bytes, string extension) =>
        extension switch
        {
            ".woff2" => bytes.StartsWith("wOF2"u8),
            ".woff" => bytes.StartsWith("wOFF"u8),
            ".ttf" => bytes.StartsWith((ReadOnlySpan<byte>)[0x00, 0x01, 0x00, 0x00])
                || bytes.StartsWith("true"u8),
            ".otf" => bytes.StartsWith("OTTO"u8)
                || bytes.StartsWith((ReadOnlySpan<byte>)[0x00, 0x01, 0x00, 0x00]),
            ".png" => bytes.StartsWith(
                (ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]
            ),
            ".gif" => bytes.StartsWith("GIF87a"u8) || bytes.StartsWith("GIF89a"u8),
            ".jpg" or ".jpeg" => bytes.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]),
            ".webp" => bytes.Length >= 12
                && bytes.StartsWith("RIFF"u8)
                && bytes[8..12].SequenceEqual("WEBP"u8),
            _ => false,
        };

    public async Task<byte[]?> ExportAsync(
        string pluginKey,
        PluginExportRequest request,
        CancellationToken ct
    )
    {
        if (manager.FolderOf(pluginKey) is not { } folder || !Directory.Exists(folder))
            return null;

        if (!KeyPattern().IsMatch(request.Key) || request.Key == IHotelDataPlugin.KEY)
            throw new ArgumentException(
                $"The new plugin's key \"{request.Key}\" is letters, digits, '.', '_' and '-', and not {IHotelDataPlugin.KEY}.",
                nameof(request)
            );

        if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Version))
            throw new ArgumentException(
                "The new plugin has a name and a version.",
                nameof(request)
            );

        var manifest = PluginHelpers.ReadManifest(folder);

        if (!PluginHelpers.IsDataOnly(manifest))
            throw new ArgumentException(
                $"{manifest.Name} has code: share its author's package instead. Only data plugins (themes, the hotel's changes) are made here.",
                nameof(pluginKey)
            );

        var files = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            var name = Path.GetRelativePath(folder, file).Replace('\\', '/');

            // The hotel's own bookkeeping, and anything hidden, stay here.
            if (name is MANIFEST or PATCHES || name.Split('/').Any(x => x.StartsWith('.')))
                continue;

            files[name] = await File.ReadAllBytesAsync(file, ct).ConfigureAwait(false);
        }

        if (string.Equals(pluginKey, IHotelDataPlugin.KEY, StringComparison.OrdinalIgnoreCase))
            files[$"{WWWROOT}/{TEMPLATE_PATCHES}"] = Served(
                await ReadAsync(ct).ConfigureAwait(false)
            );

        if (host.GetService<IPluginStyles>() is { } styles)
            await BakeStylesAsync(pluginKey, folder, styles, files, ct).ConfigureAwait(false);

        files[MANIFEST] = JsonSerializer.SerializeToUtf8Bytes(
            new PluginManifest
            {
                Key = request.Key,
                Name = request.Name.Trim(),
                Version = request.Version.Trim(),
                Author = string.IsNullOrWhiteSpace(request.Author)
                    ? manifest.Author
                    : request.Author.Trim(),
                ApiVersion = PluginApi.VERSION,
            },
            new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = System
                    .Text
                    .Json
                    .Serialization
                    .JsonIgnoreCondition
                    .WhenWritingNull,
            }
        );

        using var zip = new MemoryStream();

        using (var archive = new ZipArchive(zip, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, bytes) in files.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
                var stream = entry.Open();
                await using (stream.ConfigureAwait(false))
                    await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            }

        return zip.ToArray();
    }

    /// <summary>
    /// The theme's <c>theme.json</c> with each style the hotel changed defaulting to the hotel's
    /// value (one it inherits redeclared with only its default; the hotel's own plugin, with no
    /// file, gets one that extends the base theme); an image staff uploaded to the hotel's plugin
    /// goes into the new plugin's <c>wwwroot/styles/</c>, named by its path there.
    /// </summary>
    private async Task BakeStylesAsync(
        string pluginKey,
        string folder,
        IPluginStyles styles,
        Dictionary<string, byte[]> files,
        CancellationToken ct
    )
    {
        var list = await styles.ListAsync(pluginKey, folder, ct).ConfigureAwait(false);

        if (!files.TryGetValue(THEME, out var theme) && !list.Any(x => x.Changed))
            return;

        var root = theme is null
            ? new JsonObject { ["extends"] = BASE_THEME, ["styles"] = new JsonObject() }
            : JsonNode.Parse(
                theme,
                documentOptions: new JsonDocumentOptions
                {
                    AllowTrailingCommas = true,
                    CommentHandling = JsonCommentHandling.Skip,
                }
            )!;
        var hotelPrefix = $"/gamedata/plugins/{IHotelDataPlugin.KEY}/";

        root["styles"] ??= new JsonObject();

        foreach (var style in list.Where(x => x.Changed))
        {
            if (style.Value is not { } value)
                continue;

            if (style.Property.Type == ThemeStyleType.Image)
            {
                var at = value.IndexOf(hotelPrefix, StringComparison.Ordinal);
                var path = at < 0 ? null : value[(at + hotelPrefix.Length)..].Split('?')[0];

                if (path is not null && ClientModuleFile(path) is { } file && File.Exists(file))
                {
                    value = $"styles/{Path.GetFileName(path)}";
                    files[$"{WWWROOT}/{value}"] = await File.ReadAllBytesAsync(file, ct)
                        .ConfigureAwait(false);
                }
            }

            if (root["styles"]![style.Property.Name] is not JsonObject declared)
                root["styles"]![style.Property.Name] = declared = [];

            declared["default"] = value;
        }

        files[THEME] = JsonSerializer.SerializeToUtf8Bytes(root, JSON);
    }

    private string? ClientModuleFile(string path)
    {
        var full = Path.GetFullPath(Path.Combine(Folder, WWWROOT, Uri.UnescapeDataString(path)));

        return full.StartsWith(
            Path.GetFullPath(Path.Combine(Folder, WWWROOT)),
            StringComparison.OrdinalIgnoreCase
        )
            ? full
            : null;
    }

    /// <summary>
    /// Changes the patch list, writes it and the file clients load, and loads the hotel's plugin
    /// again; answers what <paramref name="change"/> did (false: nothing was written).
    /// </summary>
    private async Task<bool> ChangeAsync(Func<List<HotelPatch>, bool> change, CancellationToken ct)
    {
        await _changes.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var list = await ReadAsync(ct).ConfigureAwait(false);

            if (!change(list))
                return false;

            Ensure();

            await File.WriteAllBytesAsync(
                    Path.Combine(Folder, PATCHES),
                    JsonSerializer.SerializeToUtf8Bytes(new { patches = list }, JSON),
                    ct
                )
                .ConfigureAwait(false);
            await File.WriteAllBytesAsync(
                    Path.Combine(Folder, WWWROOT, TEMPLATE_PATCHES),
                    Served(list),
                    ct
                )
                .ConfigureAwait(false);

            // Its patch file's address is by hash: loaded again, clients are given the new one.
            await manager.ReloadAsync(IHotelDataPlugin.KEY, ct).ConfigureAwait(false);

            return true;
        }
        finally
        {
            _changes.Release();
        }
    }

    /// <summary>The file clients load (nitro-next <c>docs/template-patches.md</c>): the enabled patches, without staff's names.</summary>
    private static byte[] Served(IEnumerable<HotelPatch> list) =>
        JsonSerializer.SerializeToUtf8Bytes(
            new
            {
                patches = list.Where(x => !x.Disabled)
                    .Select(x =>
                        x.Order is { } order
                            ? (object)
                                new
                                {
                                    template = x.Template,
                                    order,
                                    ops = x.Ops,
                                }
                            : new { template = x.Template, ops = x.Ops }
                    ),
            },
            JSON
        );

    private async Task<List<HotelPatch>> ReadAsync(CancellationToken ct)
    {
        var path = Path.Combine(Folder, PATCHES);

        if (!File.Exists(path))
            return [];

        var file = await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);

        return JsonSerializer.Deserialize<PatchFile>(file, JSON)?.Patches ?? [];
    }

    /// <summary>Makes the hotel's plugin's folder when there is none; true when it did.</summary>
    private bool Ensure()
    {
        Directory.CreateDirectory(Path.Combine(Folder, WWWROOT));

        var manifest = Path.Combine(Folder, MANIFEST);

        if (File.Exists(manifest))
            return false;

        File.WriteAllBytes(
            manifest,
            JsonSerializer.SerializeToUtf8Bytes(
                new
                {
                    Name = "Hotel changes",
                    Key = IHotelDataPlugin.KEY,
                    Version = "1.0.0",
                    Author = "Staff",
                    ApiVersion = PluginApi.VERSION,
                },
                new JsonSerializerOptions { WriteIndented = true }
            )
        );

        return true;
    }

    private sealed record PatchFile(List<HotelPatch> Patches);

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex(@"^[A-Za-z0-9_\-/.]{1,200}$")]
    private static partial Regex FilePattern();

    [GeneratedRegex(@"^[A-Za-z0-9_\-][A-Za-z0-9_\-.]{0,99}$")]
    private static partial Regex ImageNamePattern();
}
