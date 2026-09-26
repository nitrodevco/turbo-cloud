using System;
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
    public static IServiceCollection AddTurboDatabaseContext(
        this IServiceCollection services,
        HostApplicationBuilder builder
    )
    {
        services.Configure<DatabaseConfig>(
            builder.Configuration.GetSection(DatabaseConfig.SECTION_NAME)
        );

        services.AddDbContextFactory<TurboDbContext>(
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

        options.UseMySql(
            connectionString,
            ServerVersion.AutoDetect(connectionString),
            configureMySql
        );

        if (!dbConfig.LoggingEnabled)
            options.UseLoggerFactory(NullLoggerFactory.Instance);
    }
}
