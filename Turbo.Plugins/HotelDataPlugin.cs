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

    private static readonly string[] IMAGES = [".png", ".gif", ".jpg", ".jpeg", ".webp"];

    private static readonly JsonSerializerOptions JSON = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    /// <summary>Held while the hotel's plugin's files change: one change at a time.</summary>
    private readonly SemaphoreSlim _changes = new(1, 1);

    private string Folder => Path.Combine(manager.Config.PluginFolderPath, IHotelDataPlugin.KEY);

    public string? FolderOf(string pluginKey) => manager.FolderOf(pluginKey);

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
    /// value; an image staff uploaded to the hotel's plugin goes into the new plugin's
    /// <c>wwwroot/styles/</c>, named by its path there.
    /// </summary>
    private async Task BakeStylesAsync(
        string pluginKey,
        string folder,
        IPluginStyles styles,
        Dictionary<string, byte[]> files,
        CancellationToken ct
    )
    {
        if (!files.TryGetValue(THEME, out var theme))
            return;

        var list = await styles.ListAsync(pluginKey, folder, ct).ConfigureAwait(false);
        var root = JsonNode.Parse(theme)!;
        var hotelPrefix = $"/gamedata/plugins/{IHotelDataPlugin.KEY}/";

        foreach (var style in list.Where(x => x.Changed))
        {
            var value = style.Value;

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

            root["styles"]![style.Property.Name]!["default"] = value;
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
}
