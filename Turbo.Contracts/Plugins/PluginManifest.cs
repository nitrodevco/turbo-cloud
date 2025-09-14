namespace Turbo.Contracts.Plugins;

public record PluginManifest(
    string Id,
    string Name,
    string Author,
    string Version,
    string EntryAssembly,
    string TablePrefix
);
