using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using MySqlConnector;
using Turbo.Database.Configuration;

namespace Turbo.Main.Settings;

/// <summary>
/// Puts the admin panel's overrides (<c>server_settings</c>) into the configuration, right above
/// the last JSON file (<c>appsettings.{Environment}.json</c>, or user secrets): the environment
/// variables and the command line the host reads after the files stay above them, so an
/// operator always has the last word. Every layer below is shadowed
/// (<see cref="ShadowedConfigurationProvider"/>), so a list or map the panel overrides is the
/// panel's alone.
/// </summary>
internal static class ServerSettingsConfiguration
{
    private const string SELECT = "SELECT `setting_path`, `value` FROM `server_settings`";

    /// <summary>
    /// Adds the overrides read by <paramref name="load"/>; the database's own by default, through
    /// the connection the configuration names now. Every module reads its options after this, so
    /// it has to come before them.
    /// </summary>
    public static void AddTo(
        IConfigurationManager configuration,
        Func<ServerSettingsLoad>? load = null
    )
    {
        if (load is null)
        {
            // The database's settings are themselves never overridden ([StartupSetting]), so the
            // connection read now is the one for good.
            var connection =
                configuration
                    .GetSection(DatabaseConfig.SECTION_NAME)
                    .Get<DatabaseConfig>()
                    ?.ConnectionString
                ?? string.Empty;

            load = () => Read(connection);
        }

        var overrides = new ServerSettingsOverrides();
        var sources = configuration.Sources;
        var lastFile = -1;

        for (var i = 0; i < sources.Count; i++)
            if (sources[i] is JsonConfigurationSource)
                lastFile = i;

        // Each change to the sources builds every provider again: the layers below are wrapped
        // first, so the overrides, added last, are read from the database once.
        for (var i = 0; i <= lastFile; i++)
            sources[i] = new ShadowedConfigurationProvider.Source(sources[i], overrides);

        sources.Insert(lastFile + 1, new ServerSettingsConfigurationSource(load, overrides));
    }

    /// <summary>The overrides in the database; none before the table is made, or without a database.</summary>
    public static ServerSettingsLoad Read(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return ServerSettingsLoad.NONE;

        try
        {
            using var connection = new MySqlConnection(connectionString);

            connection.Open();

            using var command = connection.CreateCommand();

            command.CommandText = SELECT;

            using var reader = command.ExecuteReader();
            var rows = new List<(string Path, string Value)>();

            while (reader.Read())
                rows.Add((reader.GetString(0), reader.GetString(1)));

            return new ServerSettingsLoad(rows, null);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoSuchTable)
        {
            // Not migrated yet: nothing is overridden.
            return ServerSettingsLoad.NONE;
        }
        catch (Exception ex)
            when (ex is MySqlException or ArgumentException or InvalidOperationException)
        {
            // The server can't start without its database anyway; it says so itself, and the
            // panel shows this beside the settings.
            return new ServerSettingsLoad(
                [],
                $"The panel's settings couldn't be read: {ex.Message}"
            );
        }
    }

    /// <summary>A provider as it is, out of its shadow: the file it reads, say.</summary>
    public static IConfigurationProvider Unshadowed(IConfigurationProvider provider) =>
        provider is ShadowedConfigurationProvider shadowed ? shadowed.Inner : provider;

    /// <summary>The overrides among the configuration's providers; null before they are added.</summary>
    public static ServerSettingsConfigurationProvider? Find(IConfiguration configuration) =>
        configuration is IConfigurationRoot root
            ? root.Providers.OfType<ServerSettingsConfigurationProvider>().FirstOrDefault()
            : null;

    private sealed class ServerSettingsConfigurationSource(
        Func<ServerSettingsLoad> load,
        ServerSettingsOverrides overrides
    ) : IConfigurationSource
    {
        public IConfigurationProvider Build(IConfigurationBuilder builder) =>
            new ServerSettingsConfigurationProvider(load, overrides);
    }
}
