using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

public static class PluginDiscovery
{
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
                ?? throw new FileNotFoundException(
                    $"No assembly for plugin {manifest.Key} in {pluginDir}"
                );
            asmPath = alt;
        }

        return asmPath;
    }

    public static PluginManifest ReadManifest(string dir)
    {
        var path = Path.Combine(dir, "manifest.json");

        if (!File.Exists(path))
            throw new FileNotFoundException($"Plugin manifest not found: {path}");

        try
        {
            var manifest =
                (
                    JsonSerializer.Deserialize<PluginManifest>(
                        File.ReadAllText(path),
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            ReadCommentHandling = JsonCommentHandling.Skip,
                            AllowTrailingCommas = true,
                        }
                    ) ?? throw new InvalidOperationException("Invalid manifest.json")
                )
                ?? throw new InvalidDataException($"manifest.json at {path} deserialized to null");

            if (string.IsNullOrWhiteSpace(manifest.Name))
                throw new InvalidDataException(
                    $"Plugin manifest missing required 'Name' in {path}"
                );

            if (string.IsNullOrWhiteSpace(manifest.Version))
                throw new InvalidDataException(
                    $"Plugin manifest missing required 'Version' in {path}"
                );

            if (string.IsNullOrWhiteSpace(manifest.AssemblyFile))
                throw new InvalidDataException(
                    $"Plugin manifest missing required 'AssemblyFile' in {path}"
                );

            return manifest;
        }
        catch (Exception ex)
        {
            throw new InvalidDataException(
                $"Failed to parse manifest.json for plugin at {dir}: {ex.Message}",
                ex
            );
        }
    }
}
