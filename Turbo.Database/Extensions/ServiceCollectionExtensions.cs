using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Turbo.Contracts.Plugins;
using Turbo.Database.Configuration;
using Turbo.Database.Context;
using Turbo.Database.Delegates;
using Turbo.Database.Migrations;

namespace Turbo.Database.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddTurboDatabaseContext(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<DatabaseConfig>(
            builder.Configuration.GetSection(DatabaseConfig.SECTION_NAME)
        );

        // One migrator for the emulator's tables and every plugin's, and one lock between them.
        services.AddSingleton<IMigrationLock, PostgresMigrationLock>();
        services.AddSingleton<DatabaseMigrator>();

        // Pooled: every grain turn that touches the database creates a context, and TurboDbContext
        // holds nothing but its options, so a reset context from the pool serves as a new one.
        // Consumers still take IDbContextFactory<TurboDbContext>.
        services.AddPooledDbContextFactory<TurboDbContext>(
            (sp, options) =>
                UseTurboPostgres(
                    options,
                    sp.GetRequiredService<IOptions<DatabaseConfig>>().Value,
                    npgsql => npgsql.MigrationsAssembly("Turbo.Database")
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

                UseTurboPostgres(
                    options,
                    host.GetRequiredService<IOptions<DatabaseConfig>>().Value,
                    npgsql =>
                        npgsql.MigrationsHistoryTable(
                            $"__EFMigrationsHistory_{prefix().TrimEnd('_')}"
                        )
                );
            }
        );

        return services;
    }

    // The emulator's and every plugin's contexts connect the same way; only the migrations
    // setup differs. Every table and column is snake_case, whatever the entity calls it, so a
    // name never needs quoting in SQL. With LoggingEnabled off, EF is given no logger at all, so
    // its command and change-tracking logs stay out of the host log whatever the log level
    // filters say.
    public static DbContextOptionsBuilder UseTurboPostgres(
        DbContextOptionsBuilder options,
        DatabaseConfig dbConfig,
        Action<NpgsqlDbContextOptionsBuilder>? configureNpgsql = null
    )
    {
        options
            .UseNpgsql(WithAutoPrepare(dbConfig), configureNpgsql)
            .UseSnakeCaseNamingConvention();

        if (!dbConfig.LoggingEnabled)
            options.UseLoggerFactory(NullLoggerFactory.Instance);

        return options;
    }

    /// <summary>
    /// The connection string with automatic preparation on (<see cref="DatabaseConfig.MaxAutoPrepare"/>),
    /// unless it says otherwise itself. A statement is prepared on its second use, so a query run
    /// once (an admin search, a migration) costs nothing extra.
    /// </summary>
    public static string WithAutoPrepare(DatabaseConfig dbConfig)
    {
        if (string.IsNullOrEmpty(dbConfig.ConnectionString) || dbConfig.MaxAutoPrepare <= 0)
            return dbConfig.ConnectionString;

        var builder = new NpgsqlConnectionStringBuilder(dbConfig.ConnectionString);

        if (builder.MaxAutoPrepare > 0)
            return dbConfig.ConnectionString;

        builder.MaxAutoPrepare = dbConfig.MaxAutoPrepare;
        builder.AutoPrepareMinUsages = 2;

        return builder.ConnectionString;
    }
}
