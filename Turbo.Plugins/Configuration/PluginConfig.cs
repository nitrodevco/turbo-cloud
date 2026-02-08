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
}
