using System;
using System.Collections.Generic;
using System.IO;

namespace Turbo.Plugins.Configuration;

public class PluginConfig
{
    public const string SECTION_NAME = "Turbo:Plugin";

    public string PluginFolderPath { get; init; } =
        Path.Combine(AppContext.BaseDirectory, "plugins");

    public List<string> AllowedHostServices { get; init; } =
    ["Turbo.", "Microsoft.Extensions.", "System."];

    // When enabled, the PluginManager will collect extra diagnostics on unload failures
    // (e.g. assembly lists) and write a small diagnostics file into the plugin shadow directory.
    public bool EnableDiagnostics { get; init; } = false;
}
