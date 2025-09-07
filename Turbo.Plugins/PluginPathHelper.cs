using System;
using System.IO;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

public static class PluginPathHelper
{
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
}
