namespace Turbo.Contracts.Plugins;

public record PluginRuntimeConfig(string ConnectionString, string Schema, string TablePrefix);
