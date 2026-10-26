using System;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Turbo.Contracts.Plugins;
using Turbo.Primitives.Plugins;

namespace Turbo.Plugins;

/// <summary>
/// <see cref="IThemeAuthoring"/> over the plugin folder: a new theme is a data plugin's folder there,
/// written as a package would install it, then loaded as any plugin is. A copy is the package Make a
/// plugin makes of the theme (<see cref="IHotelDataPlugin.ExportAsync"/>), unpacked under the new key,
/// with its patches' names.
/// </summary>
internal sealed partial class ThemeAuthoring(
    PluginManager manager,
    IHotelDataPlugin hotel,
    IServiceProvider host
) : IThemeAuthoring
{
    private const string MANIFEST = "manifest.json";
    private const string PATCHES = "patches.json";
    private const string THEME = "theme.json";
    private const string BASE_THEME = "habbo";
    private const string WWWROOT = PluginClientModules.WWWROOT;
    private const string TEMPLATE_PATCHES = "template-patches.json";
    private const string VERSION = "1.0.0";

    private static readonly JsonSerializerOptions JSON = new() { WriteIndented = true };

    public async Task<PluginTheme> CreateAsync(ThemeCreateRequest request, CancellationToken ct)
    {
        var key = request.Key.Trim();
        var name = request.Name.Trim();

        if (!KeyPattern().IsMatch(key))
            throw new ArgumentException(
                $"The theme's key \"{key}\" is letters, digits, '.', '_' and '-' (64 at most), starting with a letter or digit.",
                nameof(request)
            );

        if (
            string.Equals(key, IHotelDataPlugin.KEY, StringComparison.OrdinalIgnoreCase)
            || string.Equals(key, BASE_THEME, StringComparison.OrdinalIgnoreCase)
        )
            throw new ArgumentException(
                $"{key} is the key of a theme the hotel has already.",
                nameof(request)
            );

        var folder = Path.Combine(manager.Config.PluginFolderPath, key);

        if (manager.FoundFolderOf(key) is not null || Directory.Exists(folder))
            throw new ArgumentException(
                $"There is a plugin with the key {key} already: pick another.",
                nameof(request)
            );

        if (name.Length == 0)
            throw new ArgumentException("The theme has a name staff know it by.", nameof(request));

        var author = string.IsNullOrWhiteSpace(request.Author) ? "Staff" : request.Author.Trim();
        var copyOf = string.IsNullOrWhiteSpace(request.CopyOf) ? null : request.CopyOf.Trim();
        var parent = copyOf is null
            ? Parent(
                string.IsNullOrWhiteSpace(request.Extends) ? BASE_THEME : request.Extends.Trim()
            )
            : null;
        var copy = copyOf is null
            ? null
            : await CopyAsync(copyOf, key, name, author, ct).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(Path.Combine(folder, WWWROOT, "styles"));

            if (copy is not null)
            {
                await using (copy.ConfigureAwait(false))
                using (var zip = new ZipArchive(copy.Package, ZipArchiveMode.Read))
                    zip.ExtractToDirectory(folder, overwriteFiles: true);

                if (copy.Patches is { } patches)
                    await File.WriteAllBytesAsync(Path.Combine(folder, PATCHES), patches, ct)
                        .ConfigureAwait(false);

                // A copy of the hotel with nothing changed has no file of its own: it extends Habbo's.
                if (!File.Exists(Path.Combine(folder, THEME)))
                    await WriteAsync(folder, THEME, Theme(copy.Extends), ct).ConfigureAwait(false);
            }
            else
            {
                await WriteAsync(folder, MANIFEST, Manifest(key, name, author), ct)
                    .ConfigureAwait(false);
                await WriteAsync(folder, THEME, Theme(parent!), ct).ConfigureAwait(false);
            }

            if (!File.Exists(Path.Combine(folder, WWWROOT, TEMPLATE_PATCHES)))
                await WriteAsync(
                        Path.Combine(folder, WWWROOT),
                        TEMPLATE_PATCHES,
                        new JsonObject { ["patches"] = new JsonArray() },
                        ct
                    )
                    .ConfigureAwait(false);

            await manager.ReloadAsync(key, ct).ConfigureAwait(false);
        }
        catch
        {
            // One that could not be made or loaded leaves nothing behind: staff try again.
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);

            await manager.ReloadAsync(key, CancellationToken.None).ConfigureAwait(false);

            throw;
        }

        return host.GetService<IPluginStyles>()?.ThemeOf(key, folder)
            ?? new PluginTheme(key, copy?.Extends ?? parent);
    }

    /// <summary>The theme a new one extends: the base theme, or an installed theme; else refused.</summary>
    private string Parent(string key)
    {
        if (string.Equals(key, BASE_THEME, StringComparison.OrdinalIgnoreCase))
            return BASE_THEME;

        if (ThemeOf(key) is null)
            throw new ArgumentException(
                $"{key} is no theme the hotel has: a theme extends habbo, this hotel's or an installed theme.",
                nameof(key)
            );

        return key;
    }

    private PluginTheme? ThemeOf(string key) =>
        hotel.FolderOf(key) is { } folder
            ? host.GetService<IPluginStyles>()?.ThemeOf(key, folder)
            : null;

    /// <summary>
    /// The package of the theme <paramref name="source"/> as this hotel has it, under the new key, and
    /// its <c>patches.json</c> (their names, and those turned off); refused for no theme by the key.
    /// </summary>
    private async Task<Copy> CopyAsync(
        string source,
        string key,
        string name,
        string author,
        CancellationToken ct
    )
    {
        var theme =
            ThemeOf(source)
            ?? throw new ArgumentException(
                $"{source} is no theme the hotel has to copy.",
                nameof(source)
            );
        var package = await hotel
            .ExportAsync(source, new PluginExportRequest(key, name, VERSION, author), ct)
            .ConfigureAwait(false);
        var folder = hotel.FolderOf(source);
        var patches =
            folder is not null && File.Exists(Path.Combine(folder, PATCHES))
                ? await File.ReadAllBytesAsync(Path.Combine(folder, PATCHES), ct)
                    .ConfigureAwait(false)
                : null;

        // The hotel's own plugin before staff saved anything has no folder to copy: the copy starts empty.
        package ??= Empty(key, name, author);

        return new Copy(new MemoryStream(package), patches, theme.Extends ?? BASE_THEME);
    }

    private static byte[] Empty(string key, string name, string author)
    {
        using var memory = new MemoryStream();

        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry(MANIFEST);
            using var stream = entry.Open();

            JsonSerializer.Serialize(stream, Manifest(key, name, author), JSON);
        }

        return memory.ToArray();
    }

    private static JsonObject Manifest(string key, string name, string author) =>
        new()
        {
            [nameof(PluginManifest.Name)] = name,
            [nameof(PluginManifest.Key)] = key,
            [nameof(PluginManifest.Version)] = VERSION,
            [nameof(PluginManifest.Author)] = author,
            [nameof(PluginManifest.ApiVersion)] = PluginApi.VERSION,
        };

    private static JsonObject Theme(string extends) =>
        new() { ["extends"] = extends, ["styles"] = new JsonObject() };

    private static Task WriteAsync(
        string folder,
        string file,
        JsonNode content,
        CancellationToken ct
    ) => File.WriteAllTextAsync(Path.Combine(folder, file), content.ToJsonString(JSON), ct);

    /// <summary>A copy's package, its source's patch names, and the theme the source extends.</summary>
    private sealed record Copy(MemoryStream Package, byte[]? Patches, string Extends)
        : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Package.DisposeAsync();
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,63}$")]
    private static partial Regex KeyPattern();
}
