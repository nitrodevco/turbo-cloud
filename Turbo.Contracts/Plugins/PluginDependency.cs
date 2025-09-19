namespace Turbo.Contracts.Plugins;

public sealed record PluginDependency(string Key, string? MinVersion = null);
