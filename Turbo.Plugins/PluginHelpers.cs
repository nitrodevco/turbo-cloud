using System;
using System.IO;
using System.Text.Json;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

public static class PluginHelpers
{
    public static PluginManifest ReadManifest(string dir)
    {
        var path = Path.Combine(dir, "manifest.json");
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PluginManifest>(
            json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
        )!;
    }

    public static string FindEntryAssemblyPath(string shadowDir, PluginManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.EntryAssembly))
        {
            var candidate = Path.Combine(shadowDir, manifest.EntryAssembly);
            if (File.Exists(candidate))
                return candidate;
            throw new FileNotFoundException(
                $"Entry assembly '{manifest.EntryAssembly}' not found in {shadowDir}"
            );
        }

        // Fallbacks if you omit EntryAssembly (optional)
        var byId = Path.Combine(shadowDir, manifest.Id + ".dll");
        if (File.Exists(byId))
            return byId;

        var dlls = Directory.GetFiles(shadowDir, "*.dll", SearchOption.TopDirectoryOnly);
        if (dlls.Length == 1)
            return dlls[0];

        throw new InvalidOperationException($"Could not determine entry assembly in {shadowDir}");
    }

    public static string CreateShadowCopy(string sourceDir, string pluginId)
    {
        if (!Directory.Exists(sourceDir))
            throw new DirectoryNotFoundException(sourceDir);

        var root = Path.Combine(AppContext.BaseDirectory, "plugins-shadow", pluginId);
        Directory.CreateDirectory(root);

        var shadowDir = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(shadowDir);

        CopyAll(new DirectoryInfo(sourceDir), new DirectoryInfo(shadowDir));
        return shadowDir;
    }

    public static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, true);
        }
        catch
        { /* ignore */
        }
    }

    private static void CopyAll(DirectoryInfo source, DirectoryInfo target)
    {
        foreach (var file in source.GetFiles())
            file.CopyTo(Path.Combine(target.FullName, file.Name), overwrite: true);

        foreach (var dir in source.GetDirectories())
            CopyAll(dir, target.CreateSubdirectory(dir.Name));
    }
}
