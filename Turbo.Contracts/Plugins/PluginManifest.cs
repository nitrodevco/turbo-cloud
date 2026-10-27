using System.Collections.Generic;

namespace Turbo.Contracts.Plugins;

public sealed class PluginManifest
{
    public required string Name { get; init; }
    public required string Key { get; init; }
    public required string Version { get; init; }
    public required string Author { get; init; }

    /// <summary>
    /// Its server code: globs of the C# files the server compiles it from, relative to its folder
    /// (<c>*</c> and <c>?</c> within a name, <c>**</c> any folders); <c>server/**/*.cs</c> when
    /// it names none. A plugin with no source file is a <b>data plugin</b>: its folder's files only
    /// (<c>wwwroot/</c>, <c>gamedata/</c>, <c>theme.json</c>), loaded, listed and unloaded as any
    /// other, and running no code (<c>docs/plugin-data.md</c>, <c>docs/plugin-loading.md</c>).
    /// </summary>
    public List<string>? Sources { get; init; }
    public List<PluginDependency> Dependencies { get; init; } = [];
    public string? TablePrefix { get; init; }
    public bool? ExplicitlyNoTablePrefix { get; init; }

    /// <summary>
    /// The <see cref="PluginApi.VERSION"/> the plugin was built for. A server with another one
    /// refuses to install it; a plugin that does not say is installed as before.
    /// </summary>
    public int? ApiVersion { get; init; }
}
