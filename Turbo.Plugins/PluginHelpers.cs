using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Enumeration;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Turbo.Contracts.Plugins;
using Turbo.Plugins.Exceptions;

namespace Turbo.Plugins;

internal static partial class PluginHelpers
{
    private static JsonSerializerOptions _jsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static PluginManifest ReadManifest(string dir)
    {
        var path = Path.Combine(dir, "manifest.json");

        if (!File.Exists(path))
            throw new PluginManifestException(PluginManifestErrorType.NotFound, path);

        try
        {
            var manifest =
                JsonSerializer.Deserialize<PluginManifest>(
                    File.ReadAllText(path),
                    _jsonSerializerOptions
                ) ?? throw new PluginManifestException(PluginManifestErrorType.Unreadable, path);

            if (string.IsNullOrWhiteSpace(manifest.Name))
                throw new PluginManifestException(
                    PluginManifestErrorType.MissingField,
                    path,
                    nameof(manifest.Name)
                );

            if (string.IsNullOrWhiteSpace(manifest.Key))
                throw new PluginManifestException(
                    PluginManifestErrorType.MissingField,
                    path,
                    nameof(manifest.Key)
                );

            if (string.IsNullOrWhiteSpace(manifest.Version))
                throw new PluginManifestException(
                    PluginManifestErrorType.MissingField,
                    path,
                    nameof(manifest.Version)
                );

            return manifest;
        }
        catch (Exception ex) when (ex is not PluginManifestException)
        {
            throw new PluginManifestException(
                PluginManifestErrorType.Unreadable,
                path,
                innerException: ex
            );
        }
    }

    public static IReadOnlyList<PluginManifest> SortManifests(
        IReadOnlyList<PluginManifest> manifests
    )
    {
        var byKey = manifests.ToDictionary(m => m.Key, StringComparer.OrdinalIgnoreCase);
        var indeg = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var graph = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var m in manifests)
        {
            indeg[m.Key] = 0;
            graph[m.Key] = [];
        }

        foreach (var m in manifests)
        {
            foreach (var d in m.Dependencies)
            {
                if (!byKey.ContainsKey(d.Key))
                {
                    throw new PluginDependencyException(
                        PluginDependencyErrorType.Missing,
                        m.Key,
                        [d.Key]
                    );
                }

                graph[d.Key].Add(m.Key);
                indeg[m.Key]++;
            }
        }

        var q = new Queue<string>(indeg.Where(kv => kv.Value == 0).Select(kv => kv.Key));
        var order = new List<string>();

        while (q.Count > 0)
        {
            var k = q.Dequeue();
            order.Add(k);

            foreach (var n in graph[k])
                if (--indeg[n] == 0)
                    q.Enqueue(n);
        }

        if (order.Count != manifests.Count)
            throw new PluginDependencyException(PluginDependencyErrorType.Cycle);

        return [.. order.Select(k => byKey[k])];
    }

    /// <summary>
    /// The files a plugin's code is loaded from. A change to one of them is a new version of the
    /// plugin; its other files (texts, client module files) are read by reloading it. Its client
    /// module's sources are code too (<see cref="IsClientSource"/>): the module is built from them.
    /// </summary>
    public static readonly string[] CODE_FILES = ["manifest.json", "*.dll", "*.pdb", "*.deps.json"];

    public static bool IsCodeFile(string path)
    {
        var fileName = Path.GetFileName(path);

        return !string.IsNullOrWhiteSpace(fileName)
            && (
                CODE_FILES.Any(glob =>
                    FileSystemName.MatchesSimpleExpression(glob, fileName, ignoreCase: true)
                ) || IsClientSource(path)
            );
    }

    /// <summary>A file under a <c>client/src/</c> its module is built from: TypeScript, or JSON data.</summary>
    public static bool IsClientSource(string path) =>
        Path.GetExtension(path).ToLowerInvariant() is ".ts" or ".tsx" or ".json"
        && path.Replace('\\', '/').Contains("/client/src/", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The names, sizes and write times of the plugin's code files: the same while none of them
    /// changed on disk.
    /// </summary>
    public static string CodeFingerprint(string pluginDir)
    {
        var text = new StringBuilder();

        foreach (
            var file in Directory
                .EnumerateFiles(pluginDir, "*", SearchOption.AllDirectories)
                .Where(IsCodeFile)
                .Order(StringComparer.OrdinalIgnoreCase)
        )
        {
            var info = new FileInfo(file);

            text.Append(Path.GetRelativePath(pluginDir, file))
                .Append('|')
                .Append(info.Length)
                .Append('|')
                .Append(info.LastWriteTimeUtc.Ticks)
                .Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())));
    }

    /// <summary>
    /// A plugin with no <see cref="PluginManifest.AssemblyFile"/>: its files only, no code
    /// (<c>docs/plugin-data.md</c>).
    /// </summary>
    public static bool IsDataOnly(PluginManifest manifest) =>
        string.IsNullOrWhiteSpace(manifest.AssemblyFile);

    public static string GetAssemblyPath(string pluginDir, PluginManifest manifest)
    {
        var asmPath = Path.Combine(
            pluginDir,
            Path.GetFileNameWithoutExtension(manifest.AssemblyFile) + ".dll"
        );

        if (!File.Exists(asmPath))
        {
            var alt =
                Directory
                    .GetFiles(pluginDir, "*.dll")
                    .FirstOrDefault(f =>
                        Path.GetFileNameWithoutExtension(f)
                            .Contains(manifest.Key, StringComparison.OrdinalIgnoreCase)
                    )
                ?? throw new PluginAssemblyException(
                    PluginAssemblyErrorType.NotFound,
                    manifest.Key,
                    pluginDir
                );
            asmPath = alt;
        }

        return asmPath;
    }

    /// <summary>The patch file a plugin's <c>wwwroot/</c> may carry (nitro-next <c>docs/template-patches.md</c>).</summary>
    public const string TEMPLATE_PATCHES_FILE = "template-patches.json";

    /// <summary>
    /// The templates the plugin's <c>wwwroot/template-patches.json</c> patches, each with its
    /// number of ops. None without the file; a file that can't be read is logged and gives none.
    /// </summary>
    public static IReadOnlyList<(string Template, int Ops)> ReadTemplatePatches(
        string folder,
        ILogger logger
    )
    {
        var path = Path.Combine(folder, PluginClientModules.WWWROOT, TEMPLATE_PATCHES_FILE);

        if (!File.Exists(path))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(
                File.ReadAllText(path),
                new JsonDocumentOptions
                {
                    CommentHandling = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                }
            );
            var list = new List<(string, int)>();

            if (
                doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("patches", out var patches)
                && patches.ValueKind == JsonValueKind.Array
            )
                foreach (var patch in patches.EnumerateArray())
                    if (
                        patch.ValueKind == JsonValueKind.Object
                        && patch.TryGetProperty("template", out var template)
                        && template.ValueKind == JsonValueKind.String
                    )
                        list.Add(
                            (
                                template.GetString()!,
                                patch.TryGetProperty("ops", out var ops)
                                && ops.ValueKind == JsonValueKind.Array
                                    ? ops.GetArrayLength()
                                    : 0
                            )
                        );

            return list;
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning(ex, "Could not read the template patches in {Path}", path);

            return [];
        }
    }

    /// <summary>
    /// Why a plugin did not load, as its author should read it. A route, scheduled task, command,
    /// listener or handler is built once as the plugin loads, so the service provider refuses it a
    /// scoped service, the plugin's database context above all: the message says what to take
    /// instead.
    /// </summary>
    public static string LoadFailure(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (
                e is InvalidOperationException
                && (
                    e.Message.StartsWith("Cannot resolve scoped service", StringComparison.Ordinal)
                    || e.Message.StartsWith(
                        "Cannot consume scoped service",
                        StringComparison.Ordinal
                    )
                )
            )
                return $"{e.Message} Routes, scheduled tasks, commands, listeners, handlers and singletons live as long as the plugin, so they cannot hold a scoped service such as its database context: take IServiceScopeFactory and read the tables in a scope of their own per request or run (docs/database.md).";
        }

        return ex.Message;
    }
}
