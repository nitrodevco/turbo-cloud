using System.IO;
using System.Text.Json;
using Turbo.Contracts.Plugins;

namespace Turbo.Plugins;

public static class PluginManifestUtil
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
}
