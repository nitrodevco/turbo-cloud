using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
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

            if (string.IsNullOrWhiteSpace(manifest.Version))
                throw new PluginManifestException(
                    PluginManifestErrorType.MissingField,
                    path,
                    nameof(manifest.Version)
                );

            if (string.IsNullOrWhiteSpace(manifest.AssemblyFile))
                throw new PluginManifestException(
                    PluginManifestErrorType.MissingField,
                    path,
                    nameof(manifest.AssemblyFile)
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
}
