using System;
using System.IO;

namespace Turbo.Plugins.Configuration;

public class PluginConfig
{
    public const string SECTION_NAME = "Turbo:Plugin";

    public string PluginFolderPath { get; init; } =
        Path.Combine(AppContext.BaseDirectory, "plugins");
}
