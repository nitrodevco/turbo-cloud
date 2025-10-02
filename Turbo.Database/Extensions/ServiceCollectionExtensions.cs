using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
            {
                var dbConfig = sp.GetRequiredService<IOptions<DatabaseConfig>>().Value;
                var connectionString = dbConfig.ConnectionString;
                var loggingEnabled = dbConfig.LoggingEnabled;

                options.UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
                    options =>
                    {
                        options.MigrationsAssembly("Turbo.Main");
                    }
                );
            }
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

            var tablePrefix = manifest.TablePrefix;

            if (manifest.ExplicitlyNoTablePrefix ?? false)
                tablePrefix = string.Empty;
            else
            {
                if (string.IsNullOrWhiteSpace(tablePrefix))
                {
                    tablePrefix = manifest
                        .Key.Split('-')
                        .Where(part => !string.IsNullOrEmpty(part))
                        .Select(part => char.ToLowerInvariant(part[0]))
                        .ToString();
                }

                tablePrefix += "_";
            }

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
                var dbConfig = host.GetRequiredService<IOptions<DatabaseConfig>>().Value;
                var connectionString = dbConfig.ConnectionString;
                var loggingEnabled = dbConfig.LoggingEnabled;

                options.UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
                    builder =>
                    {
                        builder.MigrationsHistoryTable(
                            $"__EFMigrationsHistory_{prefix().TrimEnd('_')}"
                        );
                    }
                );
            }
        );

        return services;
    }
}
