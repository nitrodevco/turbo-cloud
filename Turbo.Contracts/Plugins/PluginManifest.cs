using System.Collections.Generic;

namespace Turbo.Contracts.Plugins;

public sealed class PluginManifest
{
    public required string Name { get; init; }
    public required string Key { get; init; }
    public required string Version { get; init; }
    public required string Author { get; init; }
    public required string AssemblyFile { get; init; }
    public List<PluginDependency> Dependencies { get; init; } = new();
    public string? TablePrefix { get; init; } // optional prefix for database tables
}
