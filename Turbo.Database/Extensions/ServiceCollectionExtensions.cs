using System;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Turbo.Contracts.Plugins;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Delegates;
using Turbo.Database.Migrations;

namespace Turbo.Database.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The server version per connection string, asked of the server once. A plugin context's
    /// options are built per scope, and <see cref="ServerVersion.AutoDetect(string)"/> opens a
    /// connection and queries the version each time it is called.
    /// </summary>
    private static readonly ConcurrentDictionary<string, Lazy<ServerVersion>> SERVER_VERSIONS = new(
        StringComparer.Ordinal
    );

    /// <summary>
    /// The configured server version when there is one, which needs no connection; otherwise
    /// the server's own, asked on a connection without the database name: the version does not
    /// depend on it, and the database may be one the server has yet to create.
    /// </summary>
    private static ServerVersion GetServerVersion(DatabaseConfig config) =>
        !string.IsNullOrWhiteSpace(config.ServerVersion)
            ? ServerVersion.Parse(config.ServerVersion)
            : SERVER_VERSIONS
                .GetOrAdd(
                    config.ConnectionString,
                    static key => new Lazy<ServerVersion>(() =>
                        ServerVersion.AutoDetect(
                            new MySqlConnectionStringBuilder(key)
                            {
                                Database = string.Empty,
                            }.ConnectionString
                        )
                    )
                )
                .Value;

    public static IServiceCollection AddTurboDatabaseContext(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<DatabaseConfig>(
            builder.Configuration.GetSection(DatabaseConfig.SECTION_NAME)
        );

        // One migrator for the emulator's tables and every plugin's, and one lock between them.
        services.AddSingleton<IMigrationLock, MySqlMigrationLock>();
        services.AddSingleton<DatabaseMigrator>();

        // Pooled: every grain turn that touches the database creates a context, and TurboDbContext
        // holds nothing but its options, so a reset context from the pool serves as a new one.
        // Consumers still take IDbContextFactory<TurboDbContext>.
        services.AddPooledDbContextFactory<TurboDbContext>(
            (sp, options) =>
                UseTurboMySql(
                    options,
                    sp.GetRequiredService<IOptions<DatabaseConfig>>().Value,
                    mysql => mysql.MigrationsAssembly("Turbo.Database")
                )
        );

        return services;
    }

    public static IServiceCollection AddPluginTablePrefix<TContext>(
        this IServiceCollection services
    )
        where TContext : DbContext
    {
        services.AddSingleton<TablePrefixProvider>(sp =>
        {
            var manifest = sp.GetRequiredService<PluginManifest>();

            // Used exactly as the manifest writes it, separator included ("tsp_"): the plugin's
            // design-time context factory builds its migrations from the same value, so deriving
            // or suffixing one here would rename tables the migrations already created.
            return () => manifest.TablePrefix ?? string.Empty;
        });

        return services;
    }

    public static IServiceCollection AddPluginDatabaseContext<TContext, TModule>(
        this IServiceCollection services
    )
        where TContext : DbContext
        where TModule : class, IPluginDbModule
    {
        services.AddPluginTablePrefix<TContext>();
        services.AddTransient<IPluginDbModule, TModule>();

        services.AddDbContext<TContext>(
            (sp, options) =>
            {
                var prefix = sp.GetRequiredService<TablePrefixProvider>();
                var host = sp.GetRequiredService<IHostServices>();

                UseTurboMySql(
                    options,
                    host.GetRequiredService<IOptions<DatabaseConfig>>().Value,
                    mysql =>
                        mysql.MigrationsHistoryTable(
                            $"__EFMigrationsHistory_{prefix().TrimEnd('_')}"
                        )
                );
            }
        );

        return services;
    }

    // The emulator's and every plugin's contexts connect the same way; only the migrations
    // setup differs. With LoggingEnabled off, EF is given no logger at all, so its command and
    // change-tracking logs stay out of the host log whatever the log level filters say.
    private static void UseTurboMySql(
        DbContextOptionsBuilder options,
        DatabaseConfig dbConfig,
        Action<MySqlDbContextOptionsBuilder> configureMySql
    )
    {
        var connectionString = dbConfig.ConnectionString;

        options.UseMySql(connectionString, GetServerVersion(dbConfig), configureMySql);

        if (!dbConfig.LoggingEnabled)
            options.UseLoggerFactory(NullLoggerFactory.Instance);
    }
}
