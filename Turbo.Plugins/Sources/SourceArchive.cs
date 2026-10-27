using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace Turbo.Plugins.Sources;

/// <summary>
/// The plugin in a repository's source archive (GitHub's zip of a tag: every file under one top
/// folder), as the package the panel's install takes: the folder holding the plugin's
/// <c>manifest.json</c>, with its files only.
/// </summary>
internal static class SourceArchive
{
    /// <summary>
    /// The plugin folder <paramref name="folder"/> names in the repository (its root as
    /// <c>""</c>), or, when it names none, the one folder with a plugin manifest. Throws
    /// <see cref="InvalidDataException"/> with what to do when there is none or several.
    /// </summary>
    public static MemoryStream PluginPackage(Stream archive, string? folder, out string found)
    {
        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        var files = zip
            .Entries.Where(e => !e.FullName.EndsWith('/'))
            .Select(e => (Entry: e, Path: InRepository(e.FullName)))
            .Where(x => x.Path is not null)
            .ToList();
        var plugins = files
            .Where(x => Path.GetFileName(x.Path) == "manifest.json" && IsPluginManifest(x.Entry))
            .Select(x => x.Path![..^"manifest.json".Length].TrimEnd('/'))
            .Order(StringComparer.Ordinal)
            .ToList();
        var wanted = folder?.Trim().Trim('/');

        if (string.IsNullOrEmpty(wanted))
            wanted = null;

        found = wanted is not null
            ? plugins.Contains(wanted, StringComparer.Ordinal)
                ? wanted
                : throw new InvalidDataException(
                    $"The repository has no plugin manifest.json in {wanted}/."
                )
            : plugins.Count == 1
                ? plugins[0]
                : throw new InvalidDataException(
                    plugins.Count == 0
                        ? "The repository has no plugin manifest.json."
                        : $"The repository has plugins in {string.Join(", ", plugins.Select(p => p.Length == 0 ? "its root" : p + "/"))}: name the folder to install."
                );

        var prefix = found.Length == 0 ? "" : found + "/";
        var package = new MemoryStream();

        using (var output = new ZipArchive(package, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (entry, path) in files)
            {
                if (!path!.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                using var from = entry.Open();
                using var to = output.CreateEntry(path[prefix.Length..]).Open();

                from.CopyTo(to);
            }
        }

        package.Position = 0;

        return package;
    }

    /// <summary>The entry's path in the repository: without the archive's top folder.</summary>
    private static string? InRepository(string entry)
    {
        var slash = entry.IndexOf('/');

        return slash < 0 ? null : entry[(slash + 1)..];
    }

    /// <summary>A manifest.json that is a plugin's (a web app's has no Key).</summary>
    private static bool IsPluginManifest(ZipArchiveEntry entry)
    {
        try
        {
            using var stream = entry.Open();
            using var json = JsonDocument.Parse(
                stream,
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }
            );

            return json.RootElement.ValueKind == JsonValueKind.Object
                && json.RootElement.EnumerateObject()
                    .Any(p => p.Name.Equals("Key", StringComparison.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
