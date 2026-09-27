using System;
using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Turbo.Contracts.Plugins;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Delegates;

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

    private static ServerVersion GetServerVersion(string connectionString) =>
        SERVER_VERSIONS
            .GetOrAdd(
                connectionString,
                static key => new Lazy<ServerVersion>(() => ServerVersion.AutoDetect(key))
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

        options.UseMySql(connectionString, GetServerVersion(connectionString), configureMySql);

        if (!dbConfig.LoggingEnabled)
            options.UseLoggerFactory(NullLoggerFactory.Instance);
    }
}
