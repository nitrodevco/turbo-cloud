using System;
using System.IO;
using Turbo.Primitives.Settings;

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

    /// <summary>
    /// How often, in minutes, the plugins linked to a GitHub repository are checked for a new
    /// release, from startup on; 0 checks only when staff ask.
    /// </summary>
    public int UpdateCheckMinutes { get; init; } = 60;

    /// <summary>
    /// How many earlier versions of each plugin are kept to roll back to: every install keeps the
    /// one it replaces, in <c>.versions</c> beside the plugins.
    /// </summary>
    public int KeepVersions { get; init; } = 3;

    /// <summary>
    /// The GitHub owners (users or organisations) plugins may be linked from; empty allows any.
    /// </summary>
    public string[] AllowedSources { get; init; } = [];

    /// <summary>GitHub's REST API; another for GitHub Enterprise.</summary>
    [UrlSetting]
    public string GitHubApiUrl { get; init; } = "https://api.github.com";

    /// <summary>
    /// The base64 AES-256 key sealing linked repositories' tokens. Empty: one is made once and
    /// kept beside the plugins as <c>.plugin-key</c>.
    /// </summary>
    [SecretSetting]
    public string SecretKey { get; init; } = "";
}
