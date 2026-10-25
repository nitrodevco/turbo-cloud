using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Turbo.Database.Migrations;

namespace Turbo.Database.Context;

/// <summary>
/// How a <see cref="PluginDbContextBase{TContent}"/> is built when no host gives it its options:
/// by <c>dotnet ef</c> in the plugin's project. The table prefix is the plugin's
/// <c>manifest.json</c>'s, found in the working folder, the plugin's build folder or a folder
/// above them, so the migrations name the tables the plugin uses once loaded. Making a migration
/// needs no database; one is used when given as <c>-- --connection "&lt;connection string&gt;"</c>
/// or in <see cref="CONNECTION_VARIABLE"/>. The server version is <see cref="SERVER_VERSION_VARIABLE"/>,
/// else <see cref="DEFAULT_SERVER_VERSION"/>.
/// </summary>
public sealed record PluginDesignTime(
    string TablePrefix,
    string? Connection,
    string ServerVersion,
    string Manifest
)
{
    public const string CONNECTION_VARIABLE = "TURBO_DB";

    public const string SERVER_VERSION_VARIABLE = "TURBO_DB_VERSION";

    public const string DEFAULT_SERVER_VERSION = "8.0.0-mysql";

    public const string CONNECTION_ARGUMENT = "--connection";

    /// <summary>What design time reads for <paramref name="context"/>, from this process.</summary>
    public static PluginDesignTime For(Type context) =>
        Read(
            [
                Directory.GetCurrentDirectory(),
                Path.GetDirectoryName(context.Assembly.Location),
                AppContext.BaseDirectory,
            ],
            Environment.GetCommandLineArgs(),
            Environment.GetEnvironmentVariable
        );

    /// <summary>
    /// The first <c>manifest.json</c> in <paramref name="folders"/> or above one, the connection
    /// in <paramref name="args"/> or the environment.
    /// </summary>
    /// <exception cref="InvalidOperationException">No manifest was found.</exception>
    public static PluginDesignTime Read(
        IEnumerable<string?> folders,
        IReadOnlyList<string> args,
        Func<string, string?> environment
    )
    {
        var manifest =
            folders
                .Where(f => !string.IsNullOrEmpty(f))
                .Select(f => FindAbove(f!))
                .FirstOrDefault(f => f is not null)
            ?? throw new InvalidOperationException(
                "No manifest.json in the working folder, the build folder or above them: run dotnet ef in the plugin's folder, where its table prefix is read from."
            );

        using var json = JsonDocument.Parse(File.ReadAllText(manifest));

        var prefix = json.RootElement.TryGetProperty("TablePrefix", out var value)
            ? value.GetString() ?? string.Empty
            : string.Empty;
        var at = args.ToList().IndexOf(CONNECTION_ARGUMENT);
        var connection = at >= 0 && at + 1 < args.Count ? args[at + 1] : null;

        return new(
            prefix,
            connection ?? NullIfEmpty(environment(CONNECTION_VARIABLE)),
            NullIfEmpty(environment(SERVER_VERSION_VARIABLE)) ?? DEFAULT_SERVER_VERSION,
            manifest
        );
    }

    /// <summary>MySQL with the plugin's migrations history table, as the host gives it.</summary>
    public void Configure(DbContextOptionsBuilder options)
    {
        var version = Microsoft.EntityFrameworkCore.ServerVersion.Parse(ServerVersion);
        var history = MigrationHelper.HistoryTableOf(TablePrefix);

        if (Connection is { } connection)
            options.UseMySql(connection, version, mysql => mysql.MigrationsHistoryTable(history));
        else
            options.UseMySql(version, mysql => mysql.MigrationsHistoryTable(history));
    }

    private static string? FindAbove(string folder)
    {
        for (var dir = new DirectoryInfo(folder); dir is not null; dir = dir.Parent)
        {
            var file = Path.Combine(dir.FullName, "manifest.json");

            if (File.Exists(file))
                return file;
        }

        return null;
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
