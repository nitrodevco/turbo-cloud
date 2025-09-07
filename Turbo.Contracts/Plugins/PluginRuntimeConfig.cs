namespace Turbo.Contracts.Plugins;

public class PluginRuntimeConfig(string connectionString, string schema, string tablePrefix)
{
    public string ConnectionString { get; } = connectionString;
    public string Schema { get; } = schema;
    public string TablePrefix { get; } = tablePrefix;

    public static PluginRuntimeConfig BuildFromManifest(
        PluginManifest manifest,
        string connection
    ) =>
        new(
            connectionString: connection,
            schema: $"plugin_{manifest.Id}".ToLowerInvariant(),
            tablePrefix: $"{manifest.Id.ToLowerInvariant()}_"
        );
}
