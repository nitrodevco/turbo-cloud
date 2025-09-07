namespace Turbo.Contracts.Plugins;

public sealed record PluginManifest(
    string Id,
    string Name,
    string Author,
    string Version,
    string EntryAssembly
);
