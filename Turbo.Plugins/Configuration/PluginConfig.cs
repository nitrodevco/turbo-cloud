using System;
using System.IO;

namespace Turbo.Plugins.Configuration;

public class PluginConfig
{
    public const string SECTION_NAME = "Turbo:Plugin";

    public string PluginFolderPath { get; init; } =
        Path.Combine(AppContext.BaseDirectory, "plugins");

    public int DebounceMs { get; init; } = 500;

    public string[] DevPluginPaths { get; init; } = [];
    public bool HotReloadEnabled { get; init; } = true;

    /// <summary>
    /// Lets the admin panel install a plugin package into <see cref="PluginFolderPath"/>, for
    /// staff given <c>admin.plugins.install</c> by name. Off by default: a package is server code
    /// that runs with the hotel's own access.
    /// </summary>
    public bool UploadEnabled { get; init; }

    /// <summary>The largest plugin package the panel takes, packed and unpacked, in bytes.</summary>
    public long MaxUploadBytes { get; init; } = 16 * 1024 * 1024;
}
