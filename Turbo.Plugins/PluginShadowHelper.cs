using System;
using System.IO;

namespace Turbo.Plugins;

public static class PluginShadowHelper
{
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
