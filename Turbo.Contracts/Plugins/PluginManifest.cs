using System.Collections.Generic;

namespace Turbo.Contracts.Plugins;

public sealed class PluginManifest
{
    public required string Name { get; init; }
    public required string Key { get; init; }
    public required string Version { get; init; }
    public required string Author { get; init; }

    /// <summary>
    /// Its server code. A plugin without one is a <b>data plugin</b>: its folder's files only
    /// (<c>wwwroot/</c>, <c>gamedata/</c>, <c>theme.json</c>), loaded, listed and unloaded as any
    /// other, and running no code (<c>docs/plugin-data.md</c>).
    /// </summary>
    public string? AssemblyFile { get; init; }
    public List<PluginDependency> Dependencies { get; init; } = [];
    public string? TablePrefix { get; init; }
    public bool? ExplicitlyNoTablePrefix { get; init; }

    /// <summary>
    /// The <see cref="PluginApi.VERSION"/> the plugin was built for. A server with another one
    /// refuses to install it; a plugin that does not say is installed as before.
    /// </summary>
    public int? ApiVersion { get; init; }

    /// <summary>The turbo-cloud ref (tag, branch or commit) the plugin was built against, for people to read.</summary>
    public string? BuiltAgainst { get; init; }
}
