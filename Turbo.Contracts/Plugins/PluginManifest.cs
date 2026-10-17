using System.Collections.Generic;

namespace Turbo.Contracts.Plugins;

public sealed class PluginManifest
{
    public required string Name { get; init; }
    public required string Key { get; init; }
    public required string Version { get; init; }
    public required string Author { get; init; }
    public required string AssemblyFile { get; init; }
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
